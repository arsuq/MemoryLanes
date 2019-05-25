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
		/// <exception cref="OutOfMemoryException">Guess what.</exception>
		public MarshalLane(int capacity) : base(capacity)
		{
			lanePtr = Marshal.AllocHGlobal(capacity);
			this.capacity = capacity;
		}

		/// <summary>
		/// Blocks a fragment of the lane.
		/// </summary>
		/// <param name="size">Number of bytes.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <param name="awaitMS">The awaitMS for each try</param>
		/// <returns>A MarshalLaneFragment or null.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MarshalLaneFragment AllocMarshalFragment(int size, int tries, int awaitMS)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr, tries, awaitMS))
			{
				var frag = new MarshalLaneFragment(
					fr.Offset, fr.Length, lanePtr, this,
					() => resetOne(fr.LaneCycle));

				return frag;
			}
			else return null;
		}

		/// <summary>
		/// Calls AllocMarshalFragment() with the same args.
		/// </summary>
		/// <returns>Null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override MemoryFragment Alloc(int size, int tries = 10, int awaitMS = 5) =>
			AllocMarshalFragment(size, tries, awaitMS);

		/// <summary>
		/// Frees the native memory.
		/// </summary>
		public override void Dispose() => destroy(false);

		void destroy(bool isGC)
		{
			if (Interlocked.CompareExchange(ref isDisposed, 1, 0) == 0)
			{
				Marshal.FreeHGlobal(lanePtr);
				lanePtr = IntPtr.Zero;
				if (!isGC) GC.SuppressFinalize(this);
				Force(true, true);
			}
		}

		/// <summary>
		/// This lane uses a finalizer to cleanup the native memory
		/// in case the object is not manually disposed and is GCed.
		/// This is very leaky!
		/// </summary>
		~MarshalLane() => destroy(true);

		public unsafe override ReadOnlySpan<byte> GetAllBytes()
		{
			byte* p = (byte*)lanePtr;
			return new Span<byte>(p, capacity);
		}

		/// <summary>
		/// The total allocated space.
		/// </summary>
		public override int LaneCapacity => capacity;

		readonly int capacity;
		IntPtr lanePtr;
	}
}