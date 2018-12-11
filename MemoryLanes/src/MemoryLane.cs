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

		public bool Alloc(int size, ref FragmentRange frag)
		{
			var result = false;
			var isLocked = false;

			Thread.MemoryBarrier();

			var CAP = LaneCapacity;

			// Quick fail
			if (isDisposed || offset + size >= CAP || isClosed > 0) return result;

			// Wait, allocations are serialized
			spinLock.Enter(ref isLocked);
			Thread.MemoryBarrier();

			var newoffset = offset + size;

			if (!isDisposed && isClosed < 1 && newoffset < CAP)
			{
				frag = new FragmentRange(offset, size);

				Interlocked.Exchange(ref offset, newoffset);
				Interlocked.Increment(ref allocations);
				Interlocked.Exchange(ref lastAllocTick, DateTime.Now.Ticks);

				result = true;
			}

			if (isLocked) spinLock.Exit();

			return result;
		}

		protected void free()
		{
			// If all fragments are disposed, reset the offset to 0
			if (Interlocked.Decrement(ref allocations) < 1)
				Interlocked.Exchange(ref offset, 0);
		}

		/// <remarks>
		/// The derived implementations should decide whether to expose this
		/// </remarks>
		protected void force(bool close, bool reset = false)
		{
			bool isLocked = false;
			spinLock.Enter(ref isLocked);

			Interlocked.Exchange(ref isClosed, close ? 1 : 0);
			if (reset) Interlocked.Exchange(ref offset, 0);

			if (isLocked) spinLock.Exit();
		}

		public abstract int LaneCapacity { get; }
		public abstract void Dispose();

		// Use Offset, Allocations and LastAllocTick to determine bad disposing behavior 

		public int Offset => Thread.VolatileRead(ref offset);
		public int Allocations => Thread.VolatileRead(ref allocations);
		public long LastAllocTick => Thread.VolatileRead(ref lastAllocTick);
		public bool IsClosed => Thread.VolatileRead(ref isClosed) > 0;

		protected bool isDisposed;
		protected int allocations;
		protected int offset;
		protected long lastAllocTick;

		SpinLock spinLock = new SpinLock();
		int isClosed;
	}
}