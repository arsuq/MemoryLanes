using System;
using System.Collections.Concurrent;
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
			TrackGhosts = 1
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

					// Just make the slot, then the derived lane will set it 
					// at 'allocations' position.
					if (Disposal == DisposalMode.TrackGhosts)
						Tracker.Append(null);

					result = true;
				}

				allocGate.Exit();
			}

			return result;
		}

		/// <summary>
		/// Triggers deallocation of ghost fragments, i.e. never disposed and GC collected. 
		/// Does nothing if the fragments are still alive. 
		/// </summary>
		/// <returns>The number of deallocations.</returns>
		public int FreeGhosts()
		{
			lock (trackLock)
			{
				int freed = 0;
				int ai = Tracker.AppendIndex;

				for (int i = 0; i < ai; i++)
				{
					var t = Tracker[i];

					if (t == null) continue;
					if (!t.TryGetTarget(out MemoryFragment f) || f == null)
					{
						resetOne(f.LaneCycle);
						Tracker[i] = null;
						freed++;
					}
				}

				return freed;
			}
		}

		/// <summary>
		/// Free is called from the fragment's Dispose method.
		/// If the lane DisposalMode is not IDispose this method does nothing.
		/// </summary>
		/// <param name="cycle">The lane cycle given to the fragment at creation time.</param>
		protected void free(int cycle, int allocation)
		{
			if (Disposal == DisposalMode.TrackGhosts)
				Tracker[allocation] = null;

			resetOne(cycle);
		}

		protected void track(MemoryFragment f, int allocationIdx)
		{
			if (Disposal == DisposalMode.TrackGhosts)
				Tracker[allocationIdx] = new WeakReference<MemoryFragment>(f);
			else throw new MemoryLaneException(MemoryLaneException.Code.IncorrectDisposalMode);
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

						// AppendIndex shift
						if (Disposal == DisposalMode.TrackGhosts)
							Tracker.MoveAppendIndex(0, true);

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
		ConcurrentArray<WeakReference<MemoryFragment>> Tracker =
			// 2d0 make these values a setting
			new ConcurrentArray<WeakReference<MemoryFragment>>(1000, 1000);

		SpinLock allocGate = new SpinLock();
		SpinLock resetGate = new SpinLock();
		object trackLock = new object();

		int isClosed;
	}
}