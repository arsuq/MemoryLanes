using System;
using System.Threading;

namespace System
{
	public struct FragmentRange
	{
		public FragmentRange(int offset, int length)
		{
			Offset = offset;
			Length = length;
		}

		public int EndOffset => Offset + Length;

		// Should be long, but Memory<T> uses int for now
		public int Offset;
		public int Length;
	}

	public abstract class MemoryLane : IDisposable
	{
		public MemoryLane(int capacity)
		{
			if (capacity < MemoryLaneSettings.MIN_CAPACITY || capacity > MemoryLaneSettings.MAX_CAPACITY)
				throw new MemoryLaneException(MemoryLaneException.Code.SizeOutOfRange);
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
				var newoffset = Offset + size;

				if (!IsClosed && newoffset < CAP)
				{
					frag = new FragmentRange(offset, size);

					Interlocked.Exchange(ref offset, newoffset);
					Interlocked.Increment(ref allocations);
					Interlocked.Exchange(ref lastAllocTick, DateTime.Now.Ticks);

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
		public bool ResetOne(long cycle)
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
		public long LaneCycle => Volatile.Read(ref laneCycle);

		public int ALLOC_AWAIT_MS = 10;

		protected bool isDisposed;
		protected int allocations;
		protected int offset;
		protected long lastAllocTick;
		protected long laneCycle;

		SpinLock allocGate = new SpinLock();
		SpinLock resetGate = new SpinLock();
		int isClosed;
	}
}