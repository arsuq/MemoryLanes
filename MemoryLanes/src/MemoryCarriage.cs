using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
    public delegate bool FragmentCtor<L, F>(L ml, int size, ref F f) where L : MemoryLane where F : struct;
    public delegate L LaneCtor<L>(int size) where L : MemoryLane;


    public class HeapHighway : MemoryCarriage<LOHLane, LOHFragment>
    {
        public HeapHighway(params int[] lanes) : base(FragMaker, LaneMaker, new MemoryLaneSettings())
        {
            Create(lanes);
        }
        public HeapHighway(MemoryLaneSettings stg, params int[] lanes) : base(FragMaker, LaneMaker, stg)
        {
            Create(lanes);
        }

        static bool FragMaker(LOHLane lane, int size, ref LOHFragment frag)
        {
            return lane.TryCreateFragment(size, ref frag);
        }
        static LOHLane LaneMaker(int size)
        {
            return new LOHLane(size);
        }
    }

    public class MappedHighway : MemoryCarriage<MMFLane, MMFFragment>
    {
        public MappedHighway(MemoryLaneSettings stg) : base(FragMaker, LaneMaker, stg) { }

        static bool FragMaker(MMFLane lane, int size, ref MMFFragment frag)
        {
            return lane.TryCreateFragment(size, ref frag);
        }
        static MMFLane LaneMaker(int size)
        {
            return new MMFLane(size);
        }
    }

    public class MemoryCarriage<L, F> where L : MemoryLane where F : struct
    {
        public MemoryCarriage(FragmentCtor<L, F> fc, LaneCtor<L> lc, MemoryLaneSettings stg)
        {
            if (stg == null || fc == null || lc == null) throw new ArgumentNullException();

            settings = stg;
            fragCtor = fc;
            laneCtor = lc;
        }

        public void Create(int count)
        {
            if (count > 0 && count < MemoryLaneSettings.MAX_COUNT)
                for (int i = 0; i < count; i++)
                    CreateLane(settings.DefaultCapacity);
        }

        public void Create(int[] laneSizes)
        {
            if (laneSizes == null || laneSizes.Length < 1)
                throw new MemoryLaneException(MemoryLaneException.Code.InitFailure, "At least one lane is required.");

            foreach (var ls in laneSizes)
                if (ls > MemoryLaneSettings.MIN_CAPACITY && ls < MemoryLaneSettings.MAX_CAPACITY)
                    CreateLane(ls);
                else throw new MemoryLaneException(MemoryLaneException.Code.SizeOutOfRange);
        }

        public F Alloc(int size)
        {
            if (Lanes == null || Lanes.Count < 1) throw new MemoryLaneException(MemoryLaneException.Code.NotInitialized);
            if (size < 0) throw new ArgumentOutOfRangeException("size");

            var frag = new F();

            // Start from the oldest lane
            for (var i = 0; i < Lanes.Count; i++)
                if (fragCtor(Lanes[i], size, ref frag))
                    return frag;

            // No luck, create a new lane
            var cap = size > settings.DefaultCapacity ? size : settings.DefaultCapacity;
            var ml = CreateLane(cap);

            if (!fragCtor(ml, size, ref frag))
                throw new MemoryLaneException(MemoryLaneException.Code.NewLaneAllocFail);

            return frag;
        }

        L CreateLane(int capacity)
        {
            var ml = laneCtor(capacity);
            Lanes.Add(ml);

            return ml;
        }

        LaneCtor<L> laneCtor;
        FragmentCtor<L, F> fragCtor;
        List<L> Lanes = new List<L>();
        MemoryLaneSettings settings;
    }
}