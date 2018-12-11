using System.Runtime.CompilerServices;

namespace System
{
	public class HeapLane : MemoryLane
	{
		public HeapLane(int capacity) : base(capacity)
		{
			lane = new byte[capacity];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCreateFragment(int size, ref HeapFragment frag)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr))
			{
				var mem = new Memory<byte>(lane, fr.Offset, fr.Length);
				frag = new HeapFragment(mem, () => free());
				return true;
			}
			else return false;
		}

		public override void Dispose() { lane = null; }
		public override int LaneCapacity => lane != null ? lane.Length : 0;

		byte[] lane;
	}
}