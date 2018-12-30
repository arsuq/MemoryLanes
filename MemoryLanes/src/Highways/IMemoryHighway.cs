using System.Collections.Generic;

namespace System
{
	public interface IMemoryHighway : IDisposable
	{
		MemoryFragment AllocFragment(int size, int awaitMS = -1);
		int GetTotalActiveFragments();
		int GetTotalCapacity();
		int GetLanesCount();
		int GetLastLaneIndex();
		void FreeGhosts();
		long LastAllocTickAnyLane { get; }
		IReadOnlyList<MemoryLane> GetLanes();
		MemoryLane this[int index] { get; }
		string FullTrace();
	}
}
