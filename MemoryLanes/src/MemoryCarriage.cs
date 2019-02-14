/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace System
{
	/// <summary>
	/// The allocation/release behavior is generalized here.
	/// </summary>
	/// <typeparam name="L">A Lane</typeparam>
	/// <typeparam name="F">The corresponding fragment type</typeparam>
	public abstract class MemoryCarriage<L, F> : IMemoryHighway, IDisposable where L : MemoryLane where F : MemoryFragment
	{
		public MemoryCarriage(MemoryLaneSettings stg)
		{
			settings = stg ?? throw new ArgumentNullException();

			if (settings.RegisterForProcessExitCleanup)
				AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();

			if (stg.MaxLanesCount < 1000)
				Lanes = new ConcurrentArray<L>(stg.MaxLanesCount, 1);
			else
			{
				var lenBlockSize = 1 + (int)Math.Sqrt(stg.MaxLanesCount);
				Lanes = new ConcurrentArray<L>(lenBlockSize, lenBlockSize);
			}
		}

		/// <summary>
		/// Creates new lanes with the default capacity from the MemoryLaneSettings. 
		/// </summary>
		/// <param name="count">Number of lanes to create.</param>
		/// <exception cref="System.MemoryLaneException">
		/// Code.MaxLanesCountReached: when the MaxLanesCountReached threshold in settings is reached AND
		/// the OnMaxLaneReached handler is either null or returns false
		/// Code.MaxTotalAllocBytesReached: when the total lanes capacity is greater than MaxTotalAllocatedBytes AND
		/// the OnMaxTotalBytesReached handler is either null or returns false, meaning "do not ignore".
		/// Code.SizeOutOfRange: when at least one of the lengths is outside the 
		/// MemoryLaneSettings.MIN_CAPACITY - MemoryLaneSettings.MAX_CAPACITY interval.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If notNullsCount is outside the 1-MemoryLaneSettings.MAX_COUNT interval </exception>
		public void Create(int count)
		{
			if (count < 1 || count > MemoryLaneSettings.MAX_COUNT) throw new ArgumentOutOfRangeException("notNullsCount");

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
		/// MemoryLaneSettings.MIN_CAPACITY - MemoryLaneSettings.MAX_CAPACITY interval.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">When the laneSizes is either null or has zero items.</exception>
		public void Create(params int[] laneSizes)
		{
			if (laneSizes == null || laneSizes.Length < 1) throw new ArgumentNullException("laneSizes");

			foreach (var ls in laneSizes)
				allocLane(ls);
		}

		/// <summary>
		/// Allocates a generic fragment with a specified length.
		/// </summary>
		/// <param name="size">The number of bytes to allocate.</param>
		/// <param name="awaitMS">The lane lock await in milliseconds, by default awaits forever (-1)</param>
		/// <returns>The fragment structure.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// If size is negative or greater than MemoryLaneSettings.MAX_CAPACITY.
		/// </exception>
		/// <exception cref="System.MemoryLaneException">
		/// Code.NotInitialized: when the lanes are not initialized.
		/// Code.NewLaneAllocFail: after an unsuccessful attempt to allocate a fragment in a dedicated new lane.
		/// One should never see this one!
		/// </exception>
		public F Alloc(int size, int awaitMS = -1)
		{
			if (Lanes == null || Lanes.Capacity == 0) throw new MemoryLaneException(MemoryLaneException.Code.NotInitialized);
			if (size < 0 || size > MemoryLaneSettings.MAX_CAPACITY) throw new ArgumentOutOfRangeException("size");

			F frag = null;

			// Start from the oldest lane
			// If awaitMS > -1 cycle around a few times before making a new lane
			var LAPS = awaitMS > -1 ? settings.NoWaitLapsBeforeNewLane : 1;
			for (var laps = 0; laps < LAPS; laps++)
				for (var i = 0; i <= Lanes.AppendIndex; i++)
				{
					var lane = Lanes[i];
					if (lane != null && createFragment(lane, size, ref frag, awaitMS))
					{
						Interlocked.Exchange(ref lastAllocTickAnyLane, DateTime.Now.Ticks);
						return frag;
					}
				}

			// No luck, create a new lane and do not publish it before getting a fragment
			var nextCapacity = settings.NextCapacity(Lanes.AppendIndex);
			var cap = size > nextCapacity ? size : nextCapacity;
			var ml = allocLane(cap, true);

			// Could be null if the MaxLanesCount or MaxBytesCount exceptions are ignored.
			// The consumer can infer that by checking if the fragment is null.
			if (ml != null)
			{
				if (!createFragment(ml, size, ref frag, awaitMS))
					throw new MemoryLaneException(
						MemoryLaneException.Code.NewLaneAllocFail,
						string.Format("Failed to allocate {0} bytes on a dedicated lane.", size));

				Lanes.Append(ml);

				Interlocked.Exchange(ref lastAllocTickAnyLane, DateTime.Now.Ticks);
			}

			return frag;
		}

		/// <summary>
		/// Allocates a memory fragment on any of the existing lanes or on a new one.
		/// By default the allocation awaits other allocations on the same lane, pass awaitMS > 0 in
		/// order to skip a lane. Note however than the MemoryLaneSettings.NoWaitLapsBeforeNewLane controls
		/// how many cycles around all lanes should be made before allocating a new lane.
		/// </summary>
		/// <param name="size">The desired buffer length.</param>
		/// <param name="awaitMS">By default the allocation awaits other allocations on the same lane.</param>
		/// <returns></returns>
		public MemoryFragment AllocFragment(int size, int awaitMS) => Alloc(size, awaitMS);

		public virtual void Dispose()
		{
			if (!Volatile.Read(ref isDisposed))
			{
				try
				{
					if (Lanes != null && Lanes.ItemsCount > 0)
						foreach (var lane in Lanes.Items())
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
		public int GetTotalActiveFragments()
		{
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
		public int GetTotalCapacity()
		{
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
		public int GetTotalFreeSpace()
		{
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
		public int GetLanesCount() => Lanes.ItemsCount;

		/// <summary>
		/// Returns the array.AppendIndex value, i.e. the furthest index in the Lanes array.
		/// </summary>
		/// <returns>The number of preallocated lanes.</returns>
		public int GetLastLaneIndex() => Lanes.AppendIndex;

		/// <summary>
		/// Creates a new List instance with the selection of all non null cells in the underlying array.
		/// This is a relatively expensive operation, depending on the array length and the AppendIndex value, so
		/// one may consider using the indexer instead.
		/// </summary>
		/// <returns>A read only list of MemoryLane objects.</returns>
		public IReadOnlyList<MemoryLane> GetLanes() => new List<MemoryLane>(Lanes.Items());

		/// <summary>
		/// Get a specific lane.
		/// </summary>
		/// <param name="index">The index must be less than the LastLaneIndex value. </param>
		/// <returns>The Lane</returns>
		public L this[int index]
		{
			get
			{
				if (index < 0 || index > Lanes.AppendIndex) throw new ArgumentOutOfRangeException("index");
				return Lanes[index];
			}
		}

		MemoryLane IMemoryHighway.this[int index] => this[index];

		int freeGhostsGate = 0;

		/// <summary>
		/// Triggers FreeGhosts() on all lanes.
		/// </summary>
		public void FreeGhosts()
		{
			if (settings.Disposal != MemoryLane.DisposalMode.TrackGhosts)
				throw new MemoryLaneException(MemoryLaneException.Code.IncorrectDisposalMode);

			if (Interlocked.CompareExchange(ref freeGhostsGate, 1, 0) < 1)
			{
				try
				{
					foreach (var lane in Lanes.Items())
						lane.FreeGhosts();
				}
				finally
				{
					Interlocked.Exchange(ref freeGhostsGate, 0);
				}
			}
		}

		public virtual string FullTrace()
		{
			var lc = Lanes.ItemsCount;
			var lines = new string[lc + 3];
			var i = 0;

			lines[i++] = $"{this.GetType().Name}";
			lines[i++] = $"Total lanes: {lc}";
			lines[i++] = $"Now-Last allocation tick: {DateTime.Now.Ticks - LastAllocTickAnyLane}";

			foreach (var l in Lanes.Items())
				lines[i++] = l.FullTrace();

			return string.Join(Environment.NewLine, lines);
		}

		protected abstract bool createFragment(L ml, int size, ref F f, int awaitMS);
		protected abstract L createLane(int size);

		L allocLane(int capacity, bool hidden = false)
		{
			var lanesTotalLength = 0;

			if (Lanes.ItemsCount + 1 > settings.MaxLanesCount)
			{
				if (settings.OnMaxLaneReached != null && settings.OnMaxLaneReached()) return null;
				else throw new MemoryLaneException(MemoryLaneException.Code.MaxLanesCountReached);
			}

			foreach (var l in Lanes.Items())
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

		public abstract StorageType Type { get; }

		protected readonly MemoryLaneSettings settings;

		/// <summary>
		/// Use to detect bad disposal behavior. 
		/// </summary>
		public long LastAllocTickAnyLane => Thread.VolatileRead(ref lastAllocTickAnyLane);

		long lastAllocTickAnyLane;

		ConcurrentArray<L> Lanes = null;

		bool isDisposed;
	}

	public enum StorageType
	{
		Unknown = 0,
		ManagedHeapLane = 1,
		MemoryMappedFileLane = 2,
		NativeHeapLane = 4,
		NativeHeapSlot = 8
	}
}
