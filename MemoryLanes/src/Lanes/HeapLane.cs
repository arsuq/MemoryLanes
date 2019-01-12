/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;

namespace System
{
	public class HeapLane : MemoryLane
	{
		public HeapLane(int capacity, DisposalMode dm) : base(capacity, dm)
		{
			lane = new byte[capacity];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCreateFragment(int size, ref HeapFragment frag, int awaitMS = -1)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr, awaitMS))
			{
				var mem = new Memory<byte>(lane, fr.Offset, fr.Length);
				frag = new HeapFragment(mem, this, () => free(laneCycle, fr.Allocation));

				if (Disposal == DisposalMode.TrackGhosts)
					track(frag, fr.Allocation);

				return true;
			}
			else return false;
		}

		public override void Dispose() { lane = null; }
		public override int LaneCapacity => lane != null ? lane.Length : 0;

		byte[] lane;
	}
}