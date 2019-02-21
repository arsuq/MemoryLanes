/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.IO;

namespace System
{
	/// <summary>
	/// A MemoryCarriage contract
	/// </summary>
	public interface IMemoryHighway : IDisposable
	{
		/// <summary>
		/// Allocates a memory fragment on any of the existing lanes or on a new one.
		/// </summary>
		/// <remarks>
		/// By default the allocation awaits other allocations on the same lane, pass awaitMS > 0 in 
		/// order to skip a lane. Note however than the HighwaySettings.NoWaitLapsBeforeNewLane controls 
		/// how many cycles around all lanes should be made before allocating a new lane.
		/// </remarks>
		/// <param name="size">The desired buffer length.</param>
		/// <param name="awaitMS">By default the allocation awaits other allocations on the same lane.</param>
		/// <returns>A new fragment.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// If size is negative or greater than HighwaySettings.MAX_CAPACITY.
		/// </exception>
		/// <exception cref="System.MemoryLaneException">
		/// Code.NotInitialized: when the lanes are not initialized.
		/// Code.NewLaneAllocFail: after an unsuccessful attempt to allocate a fragment in a dedicated new lane.
		/// One should never see this one!
		/// </exception>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		MemoryFragment AllocFragment(int size, int awaitMS = -1);

		/// <summary>
		/// Returns an aggregate of all active fragments in all lanes.
		/// </summary>
		/// <returns>The number of active fragments</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		int GetTotalActiveFragments();

		/// <summary>
		/// Sums the lengths of all lanes.
		/// </summary>
		/// <returns>The total preallocated space for the highway.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		int GetTotalCapacity();

		/// <summary>
		/// Sums the free space in all lanes.
		/// </summary>
		/// <returns>The total bytes left.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		int GetTotalFreeSpace();

		/// <summary>
		/// Gets the Lanes notNullsCount.
		/// </summary>
		/// <returns>The number of preallocated lanes.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		int GetLanesCount();

		/// <summary>
		/// Returns the array.AppendIndex value, i.e. the furthest index in the Lanes array.
		/// </summary>
		/// <returns>The number of preallocated lanes.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		int GetLastLaneIndex();

		/// <summary>
		/// Triggers FreeGhosts() on all lanes.
		/// </summary>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		void FreeGhosts();

		/// <summary>
		/// Creates a HighwayStream from the current highway,
		/// </summary>
		/// <param name="fragmentSize">The incremental memory size.</param>
		/// <returns>The Stream.</returns>
		HighwayStream CreateStream(int fragmentSize);

		/// <summary>
		/// The last allocation time. 
		/// </summary>
		long LastAllocTickAnyLane { get; }

		/// <summary>
		/// If true all public methods throw ObjectDisposedException
		/// </summary>
		bool IsDisposed { get; }

		/// <summary>
		/// When overridden returns the highway storage type.
		/// </summary>
		StorageType Type { get; }

		/// <summary>
		/// Creates a new List instance with the selection of all non null cells in the underlying array.
		/// </summary>
		/// <returns>A read only list of MemoryLane objects.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		IReadOnlyList<MemoryLane> GetLanes();

		/// <summary>
		/// Gets a specific lane.
		/// </summary>
		/// <param name="index">The index must be less than the LastLaneIndex value. </param>
		/// <returns>The Lane</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		MemoryLane this[int index] { get; }

		/// <summary>
		/// Prints all lanes' status.
		/// </summary>
		/// <returns>An info string.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		string FullTrace();
	}
}
