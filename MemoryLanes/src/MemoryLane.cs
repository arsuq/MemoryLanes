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
			spinLock.TryEnter(awaitMS, ref isLocked);

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

				spinLock.Exit();
			}

			return result;
		}

		protected void free()
		{
			// If all fragments are disposed, reset the offset to 0
			if (Interlocked.Decrement(ref allocations) < 1)
				Interlocked.Exchange(ref offset, 0);
		}

		/// <summary>
		/// Resets the offset and allocations of the lane or closes the lane or all at once (one lock).
		/// </summary>
		/// <remarks>
		/// Resetting the offset may lead to unpredictable behavior if you attempt to read or write
		/// with any active fragments. Do this only in case of leaked fragments which are
		/// unreachable and possible GCed but never properly disposed, thus still counted in lane's Allocations.
		/// </remarks>
		/// <param name="close">True to close the lane.</param>
		/// <param name="reset">True to reset the offset and the allocations to 0.</param>
		public virtual void Force(bool close, bool reset = false)
		{
			bool isLocked = false;
			spinLock.Enter(ref isLocked);

			Interlocked.Exchange(ref isClosed, close ? 1 : 0);
			if (reset)
			{
				Interlocked.Exchange(ref offset, 0);
				Interlocked.Exchange(ref allocations, 0);
			}

			if (isLocked) spinLock.Exit();
		}

		public abstract int LaneCapacity { get; }
		public abstract void Dispose();

		// Use Offset, Allocations and LastAllocTick to determine bad disposing behavior 

		public int Offset => Thread.VolatileRead(ref offset);
		public int Allocations => Thread.VolatileRead(ref allocations);
		public long LastAllocTick => Thread.VolatileRead(ref lastAllocTick);
		public bool IsClosed => Thread.VolatileRead(ref isClosed) > 0;

		public int ALLOC_AWAIT_MS = 10;

		protected bool isDisposed;
		protected int allocations;
		protected int offset;
		protected long lastAllocTick;

		SpinLock spinLock = new SpinLock();
		int isClosed;
	}
}