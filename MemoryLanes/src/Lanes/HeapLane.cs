/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;

namespace System
{
	/// <summary>
	/// A memory lane backed by the managed heap.
	/// </summary>
	public class HeapLane : MemoryLane
	{
		/// <summary>
		/// Creates a lane with the specified capacity.
		/// </summary>
		/// <param name="capacity">The amount of bytes to allocate.</param>
		/// <param name="dm">Use IDispoise.</param>
		public HeapLane(int capacity, MemoryLaneResetMode dm) : base(capacity, dm)
		{
			lane = new byte[capacity];
		}

		/// <summary>
		/// Tries to block the size amount of bytes on the remaining lane space.
		/// </summary>
		/// <param name="size">The number of bytes.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <returns>Null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HeapFragment AllocHeapFragment(int size, int tries)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr, tries))
			{
				var mem = new Memory<byte>(lane, fr.Offset, fr.Length);
				var frag = new HeapFragment(mem, this, () => free(laneCycle, fr.Allocation));

				if (ResetMode == MemoryLaneResetMode.TrackGhosts)
					track(frag, fr.Allocation);

				return frag;
			}
			else return null;
		}

		/// <summary>
		/// Calls AllocHeapFragment with the given arguments.
		/// </summary>
		/// <param name="size">The desired size.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <returns>Null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override MemoryFragment Alloc(int size, int tries) => AllocHeapFragment(size, tries);

		/// <summary>
		/// Nulls the lane.
		/// </summary>
		public override void Dispose() { lane = null; }

		/// <summary>
		/// Returns zero if the lane is null.
		/// </summary>
		public override int LaneCapacity => lane != null ? lane.Length : 0;

		byte[] lane;
	}
}