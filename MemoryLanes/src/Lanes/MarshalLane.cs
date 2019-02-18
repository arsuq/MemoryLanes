/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace System
{
	/// <summary>
	/// A memory lane using the unmanaged process heap.
	/// </summary>
	public class MarshalLane : MemoryLane
	{
		/// <summary>
		/// Creates the lane with the desired capacity.
		/// This method allocates so expect OutOfMemoryException.
		/// </summary>
		/// <param name="capacity">The number of bytes.</param>
		/// <param name="dm">The ghost tracking switch.</param>
		/// <exception cref="OutOfMemoryException">Guess what.</exception>
		public MarshalLane(int capacity, DisposalMode dm) : base(capacity, dm)
		{
			lanePtr = Marshal.AllocHGlobal(capacity);
			this.capacity = capacity;
		}

		/// <summary>
		/// Blocks a fragment of the lane.
		/// </summary>
		/// <param name="size">Number of bytes.</param>
		/// <param name="awaitMS">Waiting for the operation in milliseconds. By default is indefinitely.</param>
		/// <returns>A MarshalLaneFragment if succeeds, null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MarshalLaneFragment AllocMarshalFragment(int size, int awaitMS = -1)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr, awaitMS))
			{
				var frag = new MarshalLaneFragment(
					fr.Offset, fr.Length, lanePtr, this,
					() => free(laneCycle, fr.Allocation));

				if (Disposal == DisposalMode.TrackGhosts)
					track(frag, fr.Allocation);

				return frag;
			}
			else return null;
		}

		/// <summary>
		/// Calls AllocMarshalFragment() with the same args.
		/// </summary>
		/// <returns>Null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override MemoryFragment Alloc(int size, int awaitMS = -1) =>
			AllocMarshalFragment(size, awaitMS);

		/// <summary>
		/// Frees the native memory.
		/// </summary>
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

		/// <summary>
		/// This lane uses a finalizer to cleanup the native memory
		/// in case the object is not manually disposed and is GCed.
		/// This is very leaky!
		/// </summary>
		~MarshalLane() => destroy(true);

		/// <summary>
		/// The total allocated space.
		/// </summary>
		public override int LaneCapacity => capacity;

		readonly int capacity;
		IntPtr lanePtr;
	}
}