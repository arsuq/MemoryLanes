using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace System
{
    public struct MarshalFragment : IDisposable
    {
        public MarshalFragment(int startIdx, int length, IntPtr lane, Action dtor)
        {
            if (startIdx < 0 || length < 0) throw new ArgumentOutOfRangeException("startIdx or length");
            if (dtor == null) throw new NullReferenceException("dtor");
            if (lane == null) throw new NullReferenceException("lane");

            StartIdx = startIdx;
            Length = length;
            destructor = dtor;
            lanePtr = lane;
        }

        public void Dispose()
        {
            if (destructor != null)
            {
                destructor();
                destructor = null;
                lanePtr = IntPtr.Zero;
            }
        }

        public unsafe Span<byte> Span()
        {
            byte* p = (byte*)lanePtr;
            p += StartIdx;
            return new Span<byte>(p, Length);
        }

        public readonly int StartIdx;
        public readonly int Length;

        Action destructor;
        IntPtr lanePtr;
    }
}
