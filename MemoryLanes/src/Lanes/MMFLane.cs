using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace System
{
	public sealed class MMFLane : MemoryLane
	{
		public MMFLane(int capacity, string filename = null) : base(capacity)
		{
			laneCapacity = capacity;
			if (string.IsNullOrEmpty(filename)) FileID = string.Format("MMF-{0}K-{1}", capacity / 1024, Guid.NewGuid().ToString().Substring(0, 8));
			else FileID = filename;
			mmf = MemoryMappedFile.CreateFromFile(FileID, FileMode.CreateNew, null, capacity);
			mmva = mmf.CreateViewAccessor();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCreateFragment(int size, ref MMFFragment frag)
		{
			var fr = new FragmentRange();

			if (Alloc(size, ref fr))
			{
				frag = new MMFFragment(fr.Offset, fr.Length, mmva, () => free());
				return true;
			}
			else return false;
		}

		public override void Dispose() => destroy();

		void destroy(bool isGC = false)
		{
			if (!isDisposed)
			{
				try
				{
					if (mmva != null) mmva.Dispose();
					if (mmf != null) mmf.Dispose();
					if (!string.IsNullOrEmpty(FileID) && File.Exists(FileID)) File.Delete(FileID);
					if (!isGC) GC.SuppressFinalize(this);
				}
				catch { }
				isDisposed = true;
			}
		}

		~MMFLane() => destroy(true);

		public override int LaneCapacity => laneCapacity;
		public readonly string FileID;
		readonly int laneCapacity;
		MemoryMappedFile mmf;
		MemoryMappedViewAccessor mmva;
	}
}