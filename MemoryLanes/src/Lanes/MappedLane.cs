/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
	/// <summary>
	/// A memory lane backed by a memory mapped file. 
	/// </summary>
	public class MappedLane : MemoryLane
	{
		/// <summary>
		/// Creates a new lane with FragmentDispose mode.
		/// </summary>
		/// <param name="capacity">The lane length.</param>
		public MappedLane(int capacity) : this(capacity, null) { }

		/// <summary>
		/// Creates a new lane.
		/// </summary>
		/// <param name="capacity">The length in bytes.</param>
		/// <param name="filename">If not provided it's auto generated as MMF-#KB-ID</param>
		/// <param name="dm">Toggle lost fragments tracking</param>
		public MappedLane(int capacity, string filename) : base(capacity)
		{
			laneCapacity = capacity;
			if (string.IsNullOrEmpty(filename)) FileID = string.Format("MMF-{0}K-{1}", capacity / 1024, Guid.NewGuid().ToString().Substring(0, 8));
			else FileID = filename;
			mmf = MemoryMappedFile.CreateFromFile(FileID, FileMode.CreateNew, null, capacity);
			mmva = mmf.CreateViewAccessor();
		}

		/// <summary>
		/// Tries to allocate the desired amount of bytes on the remaining lane space.
		/// </summary>
		/// <param name="size">The length in bytes.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <param name="awaitMS">The awaitMS for each try</param>
		/// <returns>Null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MappedFragment AllocMappedFragment(int size, int tries, int awaitMS)
		{
			var fr = new FragmentRange();

			return Alloc(size, ref fr, tries, awaitMS) ?
				new MappedFragment(fr.Offset, fr.Length, mmva, this, () => resetOne(fr.LaneCycle)) :
				null;
		}

		/// <summary>
		/// Calls AllocMappedFragment() with the given args.
		/// </summary>
		/// <param name="size">The length in bytes</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <param name="awaitMS">The awaitMS for each try</param>
		/// <returns>A casted MappedFragment if succeeds, null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override MemoryFragment Alloc(int size, int tries, int awaitMS)
			=> AllocMappedFragment(size, tries, awaitMS);

		/// <summary>
		/// Deletes the mapped file.
		/// </summary>
		public override void Dispose() => destroy();

		void destroy(bool isGC = false)
		{
			if (Interlocked.CompareExchange(ref isDisposed, 1, 0) == 0)
			{
				try
				{
					if (mmva != null) mmva.Dispose();
					if (mmf != null) mmf.Dispose();
					if (!string.IsNullOrEmpty(FileID) && File.Exists(FileID)) File.Delete(FileID);
					Force(true, true);
				}
				catch (Exception) { }
				if (!isGC) GC.SuppressFinalize(this);
			}
		}

		/// <summary>
		/// The mapped lane uses a finalizer to delete the file
		/// in case it's not properly disposed.
		/// </summary>
		~MappedLane() => destroy(true);

		public unsafe override ReadOnlySpan<byte> GetAllBytes()
		{
			byte* p = null;
			try
			{
				mmva.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
				return new Span<byte>(p, laneCapacity);
			}
			finally
			{
				if (p != null)
					mmva.SafeMemoryMappedViewHandle.ReleasePointer();
			}
		}

		/// <summary>
		/// The capacity set in the ctor.
		/// </summary>
		public override int LaneCapacity => laneCapacity;

		/// <summary>
		/// The memory mapped file name.
		/// </summary>
		public readonly string FileID;

		readonly int laneCapacity;
		MemoryMappedFile mmf;
		MemoryMappedViewAccessor mmva;
	}
}