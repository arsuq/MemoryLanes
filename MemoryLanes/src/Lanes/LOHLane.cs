using System.Runtime.CompilerServices;

namespace System
{
	public class LOHLane : MemoryLane
	{
		public LOHLane(int capacity) : base(capacity)
		{
			lane = new byte[capacity];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCreateFragment(int size, ref LOHFragment frag)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr))
			{
				frag = new LOHFragment(new Memory<byte>(lane, fr.Offset, fr.EndOffset), () => free());
				return true;
			}
			else return false;
		}

		public override void Dispose() { }
		public override int LaneCapacity => lane != null ? lane.Length : 0;

		byte[] lane;
	}
}