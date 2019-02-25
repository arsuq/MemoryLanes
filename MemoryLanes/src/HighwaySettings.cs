/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */


namespace System
{
	public class HighwaySettings
	{
		public HighwaySettings(int defLaneCapacity, int maxLanesCount, MemoryLaneResetMode dm)
			: this(defLaneCapacity, maxLanesCount, MAX_HIGHWAY_CAPACITY, dm) { }

		public HighwaySettings(
			int defLaneCapacity = 0,
			int maxLanesCount = MAX_LANE_COUNT,
			long maxTotalBytes = MAX_HIGHWAY_CAPACITY,
			MemoryLaneResetMode dm = MemoryLaneResetMode.FragmentDispose)
		{
			if (defLaneCapacity == 0) defLaneCapacity = DefaultLaneCapacity;

			if (defLaneCapacity > MIN_LANE_CAPACITY && defLaneCapacity < MAX_LANE_CAPACITY)
				DefaultCapacity = defLaneCapacity;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid max capacity value.");

			if (maxLanesCount > 0 || maxLanesCount <= MAX_LANE_COUNT)
				MaxLanesCount = maxLanesCount;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid lanes count.");

			if (maxTotalBytes > MIN_LANE_CAPACITY)
				MaxTotalAllocatedBytes = maxTotalBytes;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid total bytes value.");

			NextCapacity = (i) => DefaultCapacity;
			Disposal = dm;
		}

		/// <summary>
		/// Will be invoked if the MaxLanesCount threshold is reached.
		/// The MemoryCarriage would expect a boolean response indicating whether to swallow the 
		/// exception and return null as fragment or throw MemoryLaneException with code MaxLanesCountReached.
		/// </summary>
		/// <exception cref="MemoryLaneException">Code.MaxLanesCountReached</exception>
		public Func<bool> OnMaxLaneReached;

		/// <summary>
		/// A handler for the case of allocating more than MaxTotalAllocatedBytes in all lanes.
		/// Pass true in order to suppress the exception and just receive null as fragment.
		/// </summary>
		/// <exception cref="MemoryLaneException">Code.MaxTotalAllocBytesReached</exception>
		public Func<bool> OnMaxTotalBytesReached;

		/// <summary>
		/// If set, the function may specify different than the default capacity based on 
		/// the current number of lanes. By default always returns the DefaultCapacity value.
		/// </summary>
		public Func<int, int> NextCapacity;

		public const int MAX_LANE_COUNT = 1000;
		public const int MIN_LANE_CAPACITY = 1023;
		public const int MAX_LANE_CAPACITY = 2_000_000_000;
		public const long MAX_HIGHWAY_CAPACITY = 200_000_000_000;


		/// <summary>
		/// If not provided in the ctor this value will be used when allocating
		/// new lanes in the highway. 
		/// The default value is 8M.
		/// </summary>
		public static int DefaultLaneCapacity
		{
			get => def_base_capacity;
			set
			{
				if (value < MIN_LANE_CAPACITY || value > MAX_LANE_CAPACITY) throw new ArgumentOutOfRangeException();

				def_base_capacity = value;
			}
		}

		/// <summary>
		/// Controls how many full cycles around all lanes should be made and fail to enter the 
		/// lock with the specified awaitMS before creating a new lane.
		/// The default value is 2.
		/// </summary>
		public int NoWaitLapsBeforeNewLane = 2;

		/// <summary>
		/// When out of space this number of new lanes could be created simultaneously.
		/// The default value is 1, i.e. all Alloc() executions will block until the new
		/// lane is created, which may fail if the settings limits are reached.
		/// </summary>
		public int ConcurrentNewLaneAllocations = 1;

		/// <summary>
		/// The amount of time the Alloc() methid will wait for new lane before bailing.
		/// The default value is 100.
		/// </summary>
		public int NewLaneAllocationTimeoutMS = 100;

		/// <summary>
		/// If the allocator fail to find a free slice in any lane, 
		/// a new one will be created with DefaultCapacity bytes in length.
		/// </summary>
		public readonly int DefaultCapacity;

		/// <summary>
		/// Can be used with the OnMaxLaneReached handler as an alerting mechanism.
		/// </summary>
		public readonly int MaxLanesCount;

		/// <summary>
		/// This is the aggregated capacity in all lanes, not the actual active fragments.
		/// </summary>
		public readonly long MaxTotalAllocatedBytes;

		/// <summary>
		/// Will trigger a Dispose() before process exits. True by default.
		/// </summary>
		public bool RegisterForProcessExitCleanup = true;

		/// <summary>
		/// Specifies the disposal mode.
		/// </summary>
		public readonly MemoryLaneResetMode Disposal;

		static int def_base_capacity = 8_000_000;
	}
}