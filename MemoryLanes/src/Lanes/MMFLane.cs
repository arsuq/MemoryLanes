using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
    public sealed class MMFLane : MemoryLane
    {
        public MMFLane(int capacity, string filename = null) : base(capacity)
        {
            laneCapacity = capacity;
            FileID = string.IsNullOrEmpty(filename) ? Guid.NewGuid().ToString() : filename;
            mmf = MemoryMappedFile.CreateFromFile(FileID, FileMode.CreateNew, null, capacity);
            mmva = mmf.CreateViewAccessor();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCreateFragment(int size, ref MMFFragment frag)
        {
            var fr = new FragmentRange();

            if (Alloc(size, ref fr))
            {
                frag = new MMFFragment(fr.Offset, fr.Length, mmva, () => free());
                return true;
            }
            else return false;
        }

        public override void Dispose()
        {
            Destroy();
        }

        void Destroy(bool isGC = false)
        {
            if (!isDisposed)
            {
                try
                {
                    if (mmva != null) mmva.Dispose();
                    if (mmf != null) mmf.Dispose();
                    if (!isGC) GC.SuppressFinalize(this);
                }
                catch { }
                isDisposed = true;
            }
        }

        ~MMFLane()
        {
            Destroy(true);
        }

        public override int LaneCapacity => laneCapacity;
        public readonly string FileID;
        readonly int laneCapacity;
        MemoryMappedFile mmf;
        MemoryMappedViewAccessor mmva;
    }
}