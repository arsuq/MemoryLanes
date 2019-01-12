/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace System
{
	public class MarshalLane : MemoryLane
	{
		public MarshalLane(int capacity, DisposalMode dm) : base(capacity, dm)
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
				frag = new MarshalLaneFragment(
					fr.Offset, fr.Length, lanePtr, this,
					() => free(laneCycle, fr.Allocation));

				if (Disposal == DisposalMode.TrackGhosts)
					track(frag, fr.Allocation);

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