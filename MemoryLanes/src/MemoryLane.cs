using System;
using System.Collections.Generic;
using System.Threading;

namespace System
{
	public abstract class MemoryLane : IDisposable
	{
		public struct FragmentRange
		{
			public FragmentRange(int offset, int length, int allocation)
			{
				Offset = offset;
				Length = length;
				Allocation = allocation;
			}

			public int EndOffset => Offset + Length;

			// Should be long, but Memory<T> uses int for now
			public int Offset;
			public int Length;
			public int Allocation;
		}

		struct FragTracker
		{
			public FragTracker(MemoryFragment f)
			{
				FragmentRef = new WeakReference<MemoryFragment>(f, false);
				LaneCycle = f.LaneCycle;
			}

			public readonly WeakReference<MemoryFragment> FragmentRef;
			public readonly int LaneCycle;
		}

		/// <summary>
		/// Determines the way lanes deal with fragment deallocation.
		/// </summary>
		public enum DisposalMode : int
		{
			/// <summary>
			/// If the consumer forgets to call dispose on a fragment,
			/// the lane will never reset unless forced.
			/// </summary>
			IDispose = 0,
			/// <summary>
			/// The lane will track all fragments into a special collection
			/// of weak refs, so when the GC collects the non-disposed fragments
			/// the lane can deallocate the correct amount and reset.
			/// </summary>
			Reliable = 1,
			/// <summary>
			/// The fragment's dispose will do nothing. The only way to decrease 
			/// the Allocations in a lane is by calling ResetOne(). All synchronization
			/// and coordination is a consumer's responsibility. 
			/// </summary>
			ResetOnly = 2,
		}

		public MemoryLane(int capacity, DisposalMode dm)
		{
			if (capacity < MemoryLaneSettings.MIN_CAPACITY || capacity > MemoryLaneSettings.MAX_CAPACITY)
				throw new MemoryLaneException(MemoryLaneException.Code.SizeOutOfRange);

			Disposal = dm;
		}

		public bool Alloc(int size, ref FragmentRange frag, int awaitMS = -1)
		{
			var result = false;
			var isLocked = false;
			var CAP = LaneCapacity;

			// Quick fail
			if (isDisposed || Offset + size >= CAP || IsClosed) return result;

			// Wait, allocations are serialized
			allocGate.TryEnter(awaitMS, ref isLocked);

			if (isLocked)
			{
				// Volatile read
				var oldoffset = Offset;
				var newoffset = oldoffset + size;

				if (!IsClosed && newoffset < CAP)
				{
					Interlocked.Exchange(ref offset, newoffset);
					Interlocked.Increment(ref allocations);
					Interlocked.Exchange(ref lastAllocTick, DateTime.Now.Ticks);

					frag = new FragmentRange(oldoffset, size, allocations);

					result = true;
				}

				allocGate.Exit();
			}

			return result;
		}

		/// <summary>
		/// Decrements the Allocations counter if the passed LaneCycle matches the current LaneCycle value. 
		/// When the Allocations reach 0 the offset is reset and the LaneCycle incremented.
		/// </summary>
		/// <remarks>
		/// This methods is exposed for consumers implementing a reliable disposal by
		/// tracking the fragments with WeakReferenes and could detect lost non disposed fragments,
		/// which keep a lane from resetting. 
		/// </remarks>
		/// <param name="cycle">The laneCycle at the time of the fragment creation.</param>
		/// <returns>True if decrements one.</returns>
		/// <exception cref="System.MemoryLaneException">IncorrectDisposalMode if the Disposal is not ResetOnly.</exception>
		public bool ResetOne(int cycle)
		{
			if (Disposal != DisposalMode.ResetOnly)
				throw new MemoryLaneException(MemoryLaneException.Code.IncorrectDisposalMode);

			return resetOne(cycle);
		}

		/// <summary>
		/// Triggers deallocation of ghost fragments, i.e. never disposed and GC collected. 
		/// If the fragments are still alive, this method will not free their slots. 
		/// </summary>
		/// <returns>The number of deallocations.</returns>
		public int FreeGhosts()
		{
			int freed = 0;

			lock (trackLock)
			{
				var gone = new List<FragTracker>();

				foreach (var t in Tracker)
				{
					// It's gone 
					if (!t.FragmentRef.TryGetTarget(out MemoryFragment f) || f == null)
					{
						// Save one reset spinLock wait if it's the wrong lane
						if (t.LaneCycle == LaneCycle)
						{
							resetOne(f.LaneCycle);
							gone.Add(t);
						}
					}
					else if (t.LaneCycle != LaneCycle)
					{

					}
				}

				if (gone.Count > 0)
					foreach (var t in gone)
						if (Tracker.Remove(t)) freed++;
			}

			return freed;
		}

		/// <summary>
		/// Free is called from the fragment's Dispose method.
		/// If the lane DisposalMode is not IDispose this method does nothing.
		/// </summary>
		/// <param name="cycle">The lane cycle given to the fragment at creation time.</param>
		protected void free(int cycle, int allocation)
		{
			if (Disposal == DisposalMode.IDispose)
				resetOne(cycle);
		}

		protected void track(MemoryFragment f)
		{
			lock (trackLock)
			{
				Tracker.Add(new FragTracker(f));
			}
		}

		bool resetOne(int cycle)
		{
			bool isLocked = false;
			bool result = false;

			resetGate.Enter(ref isLocked);

			if (isLocked)
			{
				// Enter only if it's the correct lane cycle and there are allocations
				if (LaneCycle == cycle || Allocations > 0)
				{
					var allocs = Interlocked.Decrement(ref allocations);

					// If all fragments are disposed, reset the offset to 0 and change the laneCycle.
					if (allocs == 0)
					{
						Interlocked.Exchange(ref offset, 0);
						Interlocked.Increment(ref laneCycle);

						result = true;
					}
					else if (allocs > 0) result = true;
					else throw new MemoryLaneException(MemoryLaneException.Code.LaneNegativeReset);
				}

				resetGate.Exit();
			}

			return result;
		}

		/// <summary>
		/// Resets the offset and allocations of the lane or closes the lane or all at once (one lock).
		/// </summary>
		/// <remarks>
		/// Resetting the offset may lead to unpredictable behavior if you attempt to read or write
		/// with any active fragments. Do this only in case of leaked fragments which are
		/// unreachable and possibly GCed, but never properly disposed and still counted in lane's Allocations.
		/// </remarks>
		/// <param name="close">True to close the lane.</param>
		/// <param name="reset">True to reset the offset and the allocations to 0.</param>
		public void Force(bool close, bool reset = false)
		{
			bool isLocked = false;
			allocGate.Enter(ref isLocked);

			Interlocked.Exchange(ref isClosed, close ? 1 : 0);
			if (reset)
			{
				Interlocked.Exchange(ref offset, 0);
				Interlocked.Exchange(ref allocations, 0);
			}

			if (isLocked) allocGate.Exit();
		}

		/// <summary>
		/// Traces the Allocations, Capacity, Offset, LasrtAllocTick and IsClosed properties.
		/// </summary>
		/// <returns>A formatted string: [offset/cap #allocations LA:lastAllocTick on/off]</returns>
		public virtual string FullTrace() =>
			$"[{Offset}/{LaneCapacity} #{Allocations} T{DateTime.Now.Ticks - LastAllocTick} {(IsClosed ? "off" : "on")}]";

		public abstract int LaneCapacity { get; }
		public abstract void Dispose();

		// Use Offset, Allocations and LastAllocTick to determine bad disposing behavior 

		public int Offset => Volatile.Read(ref offset);
		public int Allocations => Volatile.Read(ref allocations);
		public long LastAllocTick => Volatile.Read(ref lastAllocTick);
		public bool IsClosed => Volatile.Read(ref isClosed) > 0;
		public int LaneCycle => Volatile.Read(ref laneCycle);
		public bool IsDisposed => Volatile.Read(ref isDisposed);

		public int ALLOC_AWAIT_MS = 10;

		protected bool isDisposed;
		protected int allocations;
		protected int offset;
		protected long lastAllocTick;
		protected int laneCycle;

		public readonly DisposalMode Disposal;

		// The Tracker index is the Allocation index of the fragment
		List<FragTracker> Tracker = new List<FragTracker>();

		SpinLock allocGate = new SpinLock();
		SpinLock resetGate = new SpinLock();
		object trackLock = new object();

		int isClosed;
	}
}