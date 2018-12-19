using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace System
{
	public class MarshalLane : MemoryLane
	{
		public MarshalLane(int capacity) : base(capacity)
		{
			lanePtr = Marshal.AllocHGlobal(capacity);
			Capacity = capacity;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCreateFragment(int size, ref MarshalLaneFragment frag, int awaitMS = -1)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr, awaitMS))
			{
				frag = new MarshalLaneFragment(fr.Offset, fr.Length, lanePtr, this);
				return true;
			}
			else return false;
		}

		public override void Dispose() => destroy(false);

		void destroy(bool isGC)
		{
			if (!Volatile.Read(ref isDisposed))
			{
				Marshal.FreeHGlobal(lanePtr);
				Volatile.Write(ref isDisposed, true);
				lanePtr = IntPtr.Zero;
				if (!isGC) GC.SuppressFinalize(this);
			}
		}

		~MarshalLane() => destroy(true);

		public override int LaneCapacity => Capacity;
		public readonly int Capacity;

		IntPtr lanePtr;
	}
}