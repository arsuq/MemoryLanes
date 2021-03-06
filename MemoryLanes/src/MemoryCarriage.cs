/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace System
{
	/// <summary>
	/// The allocation/release behavior is generalized here.
	/// </summary>
	/// <typeparam name="L">A Lane</typeparam>
	/// <typeparam name="F">The corresponding fragment type</typeparam>
	public abstract class MemoryCarriage<L, F> : IMemoryHighway, IDisposable where L : MemoryLane where F : MemoryFragment
	{
		public MemoryCarriage(HighwaySettings stg)
		{
			settings = stg ?? throw new ArgumentNullException();

			if (settings.RegisterForProcessExitCleanup)
				AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();

			Lanes = new Tesseract<L>();

			noluckGate = new SemaphoreSlim(settings.ConcurrentNewLaneAllocations, settings.ConcurrentNewLaneAllocations);
		}

		/// <summary>
		/// Creates new lanes with the default capacity from the HighwaySettings. 
		/// </summary>
		/// <param name="count">Number of lanes to create.</param>
		/// <exception cref="System.MemoryLaneException">
		/// Code.MaxLanesCountReached: when the MaxLanesCountReached threshold in settings is reached AND
		/// the OnMaxLaneReached handler is either null or returns false
		/// Code.MaxTotalAllocBytesReached: when the total lanes capacity is greater than MaxTotalAllocatedBytes AND
		/// the OnMaxTotalBytesReached handler is either null or returns false, meaning "do not ignore".
		/// Code.SizeOutOfRange: when at least one of the lengths is outside the 
		/// HighwaySettings.MIN_LANE_CAPACITY - HighwaySettings.MAX_LANE_CAPACITY interval.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If notNullsCount is outside the 1-HighwaySettings.MAX_LANE_COUNT interval </exception>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public void Create(int count)
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");
			if (count < 1 || count > HighwaySettings.MAX_LANE_COUNT) throw new ArgumentOutOfRangeException("notNullsCount");

			for (int i = 0; i < count; i++)
				allocLane(settings.DefaultCapacity);
		}

		/// <summary>
		/// Creates new lanes with specific capacities. 
		/// </summary>
		/// <param name="laneSizes">Lanes by length.</param>
		/// <exception cref="System.MemoryLaneException">
		/// Code.MaxLanesCountReached: when the MaxLanesCountReached threshold in settings is reached AND
		/// the OnMaxLaneReached handler is either null or returns false
		/// Code.MaxTotalAllocBytesReached: when the total lanes capacity is greater than MaxTotalAllocatedBytes AND
		/// the OnMaxTotalBytesReached handler is either null or returns false, meaning "do not ignore".
		/// Code.SizeOutOfRange: when at least one of the lengths is outside the 
		/// HighwaySettings.MIN_LANE_CAPACITY - HighwaySettings.MAX_LANE_CAPACITY interval.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">When the laneSizes is either null or has zero items.</exception>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public void Create(params int[] laneSizes)
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");
			if (laneSizes == null || laneSizes.Length < 1) throw new ArgumentNullException("laneSizes");

			foreach (var ls in laneSizes)
				allocLane(ls);
		}

		/// <summary>
		/// Allocates a generic fragment with a specified length.
		/// </summary>
		/// <param name="size">The number of bytes to allocate.</param>
		/// <param name="tries">
		/// The number of fails before switching to another lane. 
		/// If 0 the HighwaySettings.LaneAllocTries value is used.
		/// </param>
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
		public F Alloc(int size, int tries = 0, int awaitMS = 5)
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");
			if (Lanes == null || Lanes.AllocatedSlots == 0) throw new MemoryLaneException(MemoryLaneException.Code.NotInitialized);
			if (size < 0 || size > HighwaySettings.MAX_LANE_CAPACITY) throw new ArgumentOutOfRangeException("size");
			if (tries == 0) tries = settings.LaneAllocTries;

			F frag = null;
			var lanesCount = Lanes.ItemsCount;

			// Start from the oldest lane and cycle all lanes a few times before making a new lane
			for (var laps = 0; laps < settings.LapsBeforeNewLane; laps++)
				for (var i = 0; i < lanesCount; i++)
				{
					var lane = Lanes[i];

					if (lane != null && !lane.IsClosed && !lane.IsDisposed)
					{
						frag = createFragment(lane, size, tries, awaitMS);

						if (frag != null)
						{
							Volatile.Write(ref lastAllocTickAnyLane, DateTime.Now.Ticks);
							return frag;
						}
					}
				}

			// limits the concurrent lane allocations
			if (noluckGate.Wait(settings.NewLaneAllocationTimeoutMS))
			{
				// Try again, there is at least one new lane
				if (lanesCount < Lanes.ItemsCount)
				{
					noluckGate.Release();
					return Alloc(size, tries, awaitMS);
				}
				else
					try
					{
						// [i] No luck, create a new lane and do not publish it before getting a fragment

						var ccMakers = settings.ConcurrentNewLaneAllocations - noluckGate.CurrentCount;
						var nextCapacity = settings.NextCapacity(Lanes.AppendIndex + ccMakers);
						var cap = size > nextCapacity ? size : nextCapacity;
						var ml = allocLane(cap, true);

						// Could be null if the MaxLanesCount or MaxBytesCount exceptions are ignored.
						// The consumer can infer that by checking if the fragment is null.
						if (ml != null)
						{
							frag = createFragment(ml, size, tries, awaitMS);

							if (frag == null) throw new MemoryLaneException(
								MemoryLaneException.Code.NewLaneAllocFail,
								string.Format("Failed to allocate {0} bytes on a dedicated lane.", size));

							Lanes.Append(ml);
							Volatile.Write(ref lastAllocTickAnyLane, DateTime.Now.Ticks);
						}
					}
					finally { noluckGate.Release(); }
			}

			return frag;
		}

		/// <summary>
		/// Allocates a memory fragment on any of the existing lanes or on a new one.
		/// </summary>
		/// <param name="size">The desired buffer length.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
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
		public MemoryFragment AllocFragment(int size, int tries = 4, int awaitMS = 10)
			=> Alloc(size, tries, awaitMS);

		/// <summary>
		/// Calls Dispose to all lanes individually and switches the IsDisposed flag to true,
		/// All methods will throw ObjectDisposedException after that.
		/// </summary>
		public virtual void Dispose()
		{
			if (!Volatile.Read(ref isDisposed))
			{
				try
				{
					if (Lanes != null && Lanes.ItemsCount > 0)
						foreach (var lane in Lanes.NotNullItems())
							lane.Dispose();
				}
				catch { }
				Volatile.Write(ref isDisposed, true);
			}
		}

		/// <summary>
		/// Returns an aggregate of all active fragments in all lanes.
		/// </summary>
		/// <returns>The number of active fragments</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public int GetTotalActiveFragments()
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");

			// It's fine not to lock because the lanes could only increase
			var lc = Lanes.AppendIndex;
			var c = 0;
			for (int i = 0; i <= lc; i++)
				if (Lanes[i] != null)
					c += Lanes[i].Allocations;

			return c;
		}

		/// <summary>
		/// Sums the lengths of all lanes.
		/// </summary>
		/// <returns>The total preallocated space for the highway.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public int GetTotalCapacity()
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");

			var lc = Lanes.AppendIndex;
			var cap = 0;
			for (int i = 0; i <= lc; i++)
				if (Lanes[i] != null)
					cap += Lanes[i].LaneCapacity;

			return cap;
		}

		/// <summary>
		/// Sums the free space in all lanes.
		/// </summary>
		/// <returns>The total bytes left.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public int GetTotalFreeSpace()
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");

			var lc = Lanes.AppendIndex;
			var bytes = 0;
			for (int i = 0; i <= lc; i++)
			{
				var lane = Lanes[i];
				if (lane != null)
					bytes += (lane.LaneCapacity - lane.Offset);
			}

			return bytes;
		}

		/// <summary>
		/// Gets the Lanes notNullsCount.
		/// </summary>
		/// <returns>The number of preallocated lanes.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public int GetLanesCount()
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");

			return Lanes.ItemsCount;
		}

		/// <summary>
		/// Returns the array.AppendIndex value, i.e. the furthest index in the Lanes array.
		/// </summary>
		/// <returns>The number of preallocated lanes.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public int GetLastLaneIndex()
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");

			return Lanes.AppendIndex;
		}

		/// <summary>
		/// Creates a new List instance with the selection of all non null cells in the underlying array.
		/// This is a relatively expensive operation, depending on the array length and the AppendIndex value, so
		/// one may consider using the indexer instead.
		/// </summary>
		/// <returns>A read only list of MemoryLane objects.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public IReadOnlyList<MemoryLane> GetLanes()
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");

			return new List<MemoryLane>(Lanes.NotNullItems());
		}

		/// <summary>
		/// Creates a lane at specific slot with the size given by the settings NextCapacity callback.
		/// The slot must be null or Disposed.
		/// </summary>
		/// <param name="index">The index of the lane in the highway.</param>
		/// <returns>The newly created lane instance or null if fails.</returns>
		/// <exception cref="MemoryLaneException">Thresholds are reached: 
		/// MaxLanesCountReached or MaxTotalAllocBytesReached</exception>
		public MemoryLane ReopenLane(int index)
		{
			// [i] The Allocation procedure will never look back at null slots when expanding
			// thus there is no competition that may require locking.

			if (Lanes.AppendIndex < index) return null;

			var lane = Lanes[index];

			if (lane == null || lane.IsDisposed)
			{
				var newLane = allocLane(settings.NextCapacity(index), true);

				// In case that multiple threads attempt to reopen the same slot
				if (Lanes.CAS(index, newLane, lane) != lane)
					newLane.Dispose();

				return Lanes[index];
			}
			else return null;
		}

		/// <summary>
		/// Disposes a lane at index. This nulls the slot which 
		/// will be counted if the lane is disposed directly.
		/// The disposed but not nulled lanes may blow the MAX_LANES threshold at allocation.
		/// </summary>
		/// <param name="index">The slot index.</param>
		public void DisposeLane(int index)
		{
			if (Lanes.AppendIndex >= index)
			{
				var lane = Lanes.Take(index);
				if (lane != null) lane.Dispose();
			}
		}

		/// <summary>
		/// Gets a specific lane.
		/// </summary>
		/// <param name="index">The index must be less than the LastLaneIndex value. </param>
		/// <returns>The Lane</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If the index is out of bounds.</exception>
		public L this[int index]
		{
			get
			{
				if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");
				if (index < 0 || index > Lanes.AppendIndex) throw new ArgumentOutOfRangeException("index");

				return Lanes[index];
			}
		}

		/// <summary>
		/// When overridden returns the highway storage type.
		/// </summary>
		public abstract StorageType Type { get; }

		/// <summary>
		/// True if the Highway is disposed.
		/// </summary>
		public bool IsDisposed => isDisposed;

		/// <summary>
		/// Get a lane by index.
		/// </summary>
		/// <param name="index">The lane index.</param>
		/// <returns>A memory lane.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If the index is out of bounds.</exception>
		MemoryLane IMemoryHighway.this[int index] => this[index];

		/// <summary>
		/// Creates a HighwayStream from the current highway,
		/// </summary>
		/// <param name="fragmentSize">The incremental memory size.</param>
		/// <returns>The Stream.</returns>
		public HighwayStream CreateStream(int fragmentSize) => new HighwayStream(this, fragmentSize);

		/// <summary>
		/// Prints all lanes status.
		/// </summary>
		/// <returns>An info string.</returns>
		/// <exception cref="ObjectDisposedException">If the MemoryCarriage is disposed.</exception>
		public virtual string FullTrace()
		{
			if (isDisposed) throw new ObjectDisposedException("MemoryCarriage");

			var lc = Lanes.ItemsCount;
			var lines = new string[lc + 3];
			var i = 0;

			lines[i++] = $"{this.GetType().Name}";
			lines[i++] = $"Total lanes: {lc}";
			lines[i++] = $"Now-Last allocation tick: {DateTime.Now.Ticks - LastAllocTickAnyLane}";

			foreach (var l in Lanes.NotNullItems())
				lines[i++] = l.FullTrace();

			return string.Join(Environment.NewLine, lines);
		}

		protected abstract F createFragment(L ml, int size, int tries, int awaitMS);
		protected abstract L createLane(int size);

		L allocLane(int capacity, bool hidden = false)
		{
			var lanesTotalLength = 0;

			if (Lanes.ItemsCount + 1 > settings.MaxLanesCount)
			{
				if (settings.OnMaxLaneReached != null && settings.OnMaxLaneReached()) return null;
				else throw new MemoryLaneException(MemoryLaneException.Code.MaxLanesCountReached);
			}

			foreach (var l in Lanes.NotNullItems())
				lanesTotalLength += l.LaneCapacity;

			if (lanesTotalLength > settings.MaxTotalAllocatedBytes)
			{
				if (settings.OnMaxTotalBytesReached != null && settings.OnMaxTotalBytesReached()) return null;
				else throw new MemoryLaneException(MemoryLaneException.Code.MaxTotalAllocBytesReached);
			}

			var ml = createLane(capacity);

			if (!hidden) Lanes.Append(ml);

			return ml;
		}

		protected readonly HighwaySettings settings;

		/// <summary>
		/// The last allocation time. 
		/// </summary>
		public long LastAllocTickAnyLane => Thread.VolatileRead(ref lastAllocTickAnyLane);

		long lastAllocTickAnyLane;
		bool isDisposed;
		SemaphoreSlim noluckGate;
		Tesseract<L> Lanes;
	}

	/// <summary>
	/// Types of memory storage.
	/// </summary>
	public enum StorageType
	{
		Unknown = 0,
		ManagedHeapLane = 1,
		MemoryMappedFileLane = 2,
		NativeHeapLane = 4,
		VirtualLane = 8,
		NativeHeapSlot = 16,
		ManagedHeapSlot = 32
	}
}
