/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;

namespace System
{
	public interface IMemoryHighway : IDisposable
	{
		MemoryFragment AllocFragment(int size, int awaitMS = -1);
		int GetTotalActiveFragments();
		int GetTotalCapacity();
		int GetTotalFreeSpace();
		int GetLanesCount();
		int GetLastLaneIndex();
		void FreeGhosts();
		long LastAllocTickAnyLane { get; }
		IReadOnlyList<MemoryLane> GetLanes();
		MemoryLane this[int index] { get; }
		string FullTrace();
	}
}
