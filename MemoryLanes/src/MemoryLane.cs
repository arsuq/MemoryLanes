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

            // Quick fail
            if (offset + size >= LaneCapacity) return result;

            var tid = Thread.CurrentThread.ManagedThreadId;
            var id = -1;

            // Wait, allocations are serialized
            while (id != tid) id = Interlocked.CompareExchange(ref allocThreadId, tid, 0);

            // Now really check
            Thread.MemoryBarrier();
            
            var newoffset = offset + size;

            if (newoffset < LaneCapacity)
            {
                frag = new FragmentRange(offset, size);

                Interlocked.Exchange(ref offset, newoffset);
                Interlocked.Increment(ref allocations);

                result = true;
            }

            Interlocked.Exchange(ref allocThreadId, 0);

            return result;
        }

        protected void free()
        {
            // If all fragments are disposed, reset the offset to 0
            if (Interlocked.Decrement(ref allocations) < 1)
                Interlocked.Exchange(ref offset, 0);
        }

        public abstract int LaneCapacity { get; }
        public abstract void Dispose();

        protected bool isDisposed;
        protected int allocThreadId;
        protected int allocations;
        protected int offset;
    }
}