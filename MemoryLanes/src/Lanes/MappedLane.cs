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
		/// Creates a new lane with IDispose mode.
		/// </summary>
		/// <param name="capacity">The lane length.</param>
		public MappedLane(int capacity) : this(capacity, null, DisposalMode.IDispose) { }

		/// <summary>
		/// Creates a new lane.
		/// </summary>
		/// <param name="capacity">The length in bytes.</param>
		/// <param name="filename">If not provided it's auto generated as MMF-#KB-ID</param>
		/// <param name="dm">Toggle lost fragments tracking</param>
		public MappedLane(int capacity, string filename, DisposalMode dm) : base(capacity, dm)
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
		/// <param name="size">Length in bytes.</param>
		/// <param name="awaitMS">Milliseconds to wait at the lane gate. By default is -1, i.e. forever.</param>
		/// <returns>Null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MappedFragment AllocMappedFragment(int size, int awaitMS = -1)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr, awaitMS))
			{
				var frag = new MappedFragment(fr.Offset, fr.Length, mmva, this, () => free(laneCycle, fr.Allocation));

				if (Disposal == DisposalMode.TrackGhosts)
					track(frag, fr.Allocation);

				return frag;
			}
			else return null;
		}

		/// <summary>
		/// Calls AllocMappedFragment() with the given args.
		/// </summary>
		/// <returns>A casted MappedFragment if succeeds, null if fails.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override MemoryFragment Alloc(int size, int awaitMS = -1) => AllocMappedFragment(size, awaitMS);

		/// <summary>
		/// Deletes the mapped file.
		/// </summary>
		public override void Dispose() => destroy();

		void destroy(bool isGC = false)
		{
			if (!Volatile.Read(ref isDisposed))
			{
				try
				{
					if (mmva != null) mmva.Dispose();
					if (mmf != null) mmf.Dispose();
					if (!string.IsNullOrEmpty(FileID) && File.Exists(FileID)) File.Delete(FileID);
				}
				catch { }
				if (!isGC) GC.SuppressFinalize(this);
				Volatile.Write(ref isDisposed, true);
			}
		}

		/// <summary>
		/// The mapped lane uses a finalizer to delete the file
		/// in case it's not properly disposed.
		/// </summary>
		~MappedLane() => destroy(true);

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