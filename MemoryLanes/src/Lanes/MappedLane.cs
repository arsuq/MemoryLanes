using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
	public class MappedLane : MemoryLane
	{
		public MappedLane(int capacity, string filename = null) : base(capacity)
		{
			laneCapacity = capacity;
			if (string.IsNullOrEmpty(filename)) FileID = string.Format("MMF-{0}K-{1}", capacity / 1024, Guid.NewGuid().ToString().Substring(0, 8));
			else FileID = filename;
			mmf = MemoryMappedFile.CreateFromFile(FileID, FileMode.CreateNew, null, capacity);
			mmva = mmf.CreateViewAccessor();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCreateFragment(int size, ref MappedFragment frag)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr))
			{
				frag = new MappedFragment(fr.Offset, fr.Length, mmva, () => free());
				return true;
			}
			else return false;
		}

		public override void Dispose() => destroy();

		void destroy(bool isGC = false)
		{
			Thread.MemoryBarrier();

			if (!isDisposed)
			{
				try
				{
					if (mmva != null) mmva.Dispose();
					if (mmf != null) mmf.Dispose();
					if (!string.IsNullOrEmpty(FileID) && File.Exists(FileID)) File.Delete(FileID);
				}
				catch { }
				if (!isGC) GC.SuppressFinalize(this);
				isDisposed = true;
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