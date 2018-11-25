using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
	public sealed class MarshalLane : MemoryLane
	{
		public MarshalLane(int capacity) : base(capacity)
		{
			lanePtr = Marshal.AllocHGlobal(capacity);
			Capacity = capacity;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCreateFragment(int size, ref MarshalFragment frag)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr))
			{
				frag = new MarshalFragment(fr.Offset, fr.Length, lanePtr, () => free());
				return true;
			}
			else return false;
		}

		public override void Dispose()
		{
			Destroy(false);
		}

		void Destroy(bool isGC)
		{
			if (!isDisposed)
			{
				Marshal.FreeHGlobal(lanePtr);
				isDisposed = true;
				lanePtr = IntPtr.Zero;
				if (!isGC) GC.SuppressFinalize(this);
			}
		}

		~MarshalLane()
		{
			Destroy(true);
		}

		public override int LaneCapacity => Capacity;

		public readonly int Capacity;

		IntPtr lanePtr;
	}
}