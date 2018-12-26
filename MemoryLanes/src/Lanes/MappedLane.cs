using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
	public class MappedLane : MemoryLane
	{
		public MappedLane(int capacity) : this(capacity, null, DisposalMode.IDispose) { }

		public MappedLane(int capacity, string filename, DisposalMode dm) : base(capacity, dm)
		{
			laneCapacity = capacity;
			if (string.IsNullOrEmpty(filename)) FileID = string.Format("MMF-{0}K-{1}", capacity / 1024, Guid.NewGuid().ToString().Substring(0, 8));
			else FileID = filename;
			mmf = MemoryMappedFile.CreateFromFile(FileID, FileMode.CreateNew, null, capacity);
			mmva = mmf.CreateViewAccessor();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCreateFragment(int size, ref MappedFragment frag, int awaitMS = -1)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr, awaitMS))
			{
				frag = new MappedFragment(fr.Offset, fr.Length, mmva, this, () => free(laneCycle, fr.Allocation));

				if (Disposal == DisposalMode.TrackGhosts)
					track(frag, fr.Allocation);

				return true;
			}
			else return false;
		}

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

		~MappedLane() => destroy(true);

		public override int LaneCapacity => laneCapacity;
		public readonly string FileID;
		readonly int laneCapacity;
		MemoryMappedFile mmf;
		MemoryMappedViewAccessor mmva;
	}
}