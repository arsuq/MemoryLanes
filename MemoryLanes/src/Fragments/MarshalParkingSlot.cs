using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace System
{
    public struct MarshalParkingSlot : IDisposable
    {
        public MarshalParkingSlot(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException("length");

            Length = length;
            slotPtr = Marshal.AllocHGlobal(length);
        }

        public void Dispose()
        {
            if (slotPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(slotPtr);
        }

        public unsafe Span<byte> Span(bool format = false)
        {
            var span = new Span<byte>((byte*)slotPtr, Length);
            if (format) for (var i = 0; i < Length; i++) span[i] = 0;

            return span;
        }

        public readonly int Length;

        IntPtr slotPtr;
    }
}
