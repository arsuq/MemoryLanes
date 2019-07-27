using System.Collections.Generic;
using System.IO;

namespace System
{

	/// <summary>
	/// AllocFragment() produces either HeapSlot or MarshalSlot instances.
	/// There are no lanes hence no need to be disposed.
	/// </summary>
	public class VirtualHighway : IMemoryHighway
	{
		/// <summary>
		///	If allocateNativeHeapSlots is true will allocate fragments on the native heap,
		///	otherwise on the managed heap.
		/// </summary>
		/// <param name="allocateNativeHeapSlots">False by default</param>
		public VirtualHighway(bool allocateNativeHeapSlots = false)
		{
			this.allocateNativeHeapSlots = allocateNativeHeapSlots;
		}

		/// <returns>Null</returns>
		public MemoryLane this[int index] => null;

		/// <summary>
		/// Returns 0
		/// </summary>
		public long LastAllocTickAnyLane => 0;

		/// <summary>
		/// Returns false.
		/// </summary>
		public bool IsDisposed => false;

		public StorageType Type => StorageType.VirtualLane;

		/// <summary>
		/// Only the size argument is used.
		/// </summary>
		/// <param name="size">The size in bytes.</param>
		/// <param name="tries">Ignored</param>
		/// <param name="awaitMS">Ignored</param>
		/// <returns>Either a HeapSlot or a MarshalSlot</returns>
		public MemoryFragment AllocFragment(int size, int tries = 0, int awaitMS = 10)
		{
			MemoryFragment f = null;

			if (allocateNativeHeapSlots) f = new HeapSlot(size);
			else f = new MarshalSlot(size);

			return f;
		}

		/// <summary>
		/// Creates a HighwayStream
		/// </summary>
		public HighwayStream CreateStream(int fragmentSize) => new HighwayStream(this, fragmentSize);

		/// <summary>
		/// Does nothing.
		/// </summary>
		public void Dispose() { }

		/// <summary>
		/// Does nothing.
		/// </summary>
		public void DisposeLane(int index) { }

		/// <summary>
		/// Returns an empty string.
		/// </summary>
		public string FullTrace() => string.Empty;

		/// <returns>NUll</returns>
		public IReadOnlyList<MemoryLane> GetLanes() => null;
	
		/// <returns>0</returns>
		public int GetLanesCount() => 0;

		/// <returns>0</returns>
		public int GetLastLaneIndex() => 0;

		/// <returns>0</returns>
		public int GetTotalActiveFragments() => 0;

		/// <returns>0</returns>
		public int GetTotalCapacity() => 0;

		/// <returns>0</returns>
		public int GetTotalFreeSpace() => 0;

		/// <returns>Null</returns>
		public MemoryLane ReopenLane(int index) => null;

		bool allocateNativeHeapSlots;
	}
}
