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
		/// Use HighwaySettings.LapsBeforeNewLane to control how many cycles around all 
		/// lanes should be made before allocating a new one.
		/// </remarks>
		/// <param name="size">The desired buffer length.</param>
		/// <param name="tries">The number of fails before switching to another lane. 
		/// If 0, the HighwaySettings.LaneAllocTries is used. </param>
		/// <param name="awaitMS">The awaitMS for each try</param>
		/// <returns>A new fragment.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// If size is negative or greater than HighwaySettings.MAX_LANE_CAPACITY.
		/// </exception>
		/// <exception cref="System.MemoryLaneException">
		/// Code.NotInitialized: when the lanes are not initialized.
		/// Code.NewLaneAllocFail: after an unsuccessful attempt to allocate a fragment in a dedicated new lane.
		/// One should never see this one!
		/// </exception>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		MemoryFragment AllocFragment(int size, int tries = 0, int awaitMS = 10);

		/// <summary>
		/// Gets a specific lane.
		/// </summary>
		/// <param name="index">The index must be less than the LastLaneIndex value. </param>
		/// <returns>The Lane</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		MemoryLane this[int index] { get; }

		/// <summary>
		/// Creates a new List instance with the selection of all non null cells in the underlying array.
		/// </summary>
		/// <returns>A read only list of MemoryLane objects.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		IReadOnlyList<MemoryLane> GetLanes();

		/// <summary>
		/// Creates a HighwayStream from the current highway,
		/// </summary>
		/// <param name="fragmentSize">The incremental memory size.</param>
		/// <returns>The Stream.</returns>
		HighwayStream CreateStream(int fragmentSize);

		/// <summary>
		/// Creates a lane at specific slot with the size given by the settings NextCapacity callback.
		/// The slot must be null or Disposed.
		/// </summary>
		/// <param name="index">The index of the lane in the highway.</param>
		/// <returns>The newly created lane instance or null if fails.</returns>
		MemoryLane ReopenLane(int index);

		/// <summary>
		/// Disposes a lane at index. This nulls the slot which 
		/// will be counted if the lane is disposed directly.
		/// The disposed but not nulled lanes may blow the MAX_LANES threshold at allocation.
		/// </summary>
		/// <param name="index">The slot index.</param>
		void DisposeLane(int index);

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
		/// Prints all lanes' status.
		/// </summary>
		/// <returns>An info string.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		string FullTrace();
	}
}
