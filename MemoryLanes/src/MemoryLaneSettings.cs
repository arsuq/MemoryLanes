
namespace System
{
	public class MemoryLaneSettings
	{
		public MemoryLaneSettings(int defLaneCapacity, int maxLanesCount, MemoryLane.DisposalMode dm)
			: this(defLaneCapacity, maxLanesCount, defLaneCapacity * maxLanesCount, dm) { }

		public MemoryLaneSettings(
			int defLaneCapacity = 8_000_000,
			int maxLanesCount = MAX_COUNT,
			long maxTotalBytes = MAX_CAPACITY,
			MemoryLane.DisposalMode dm = MemoryLane.DisposalMode.IDispose)
		{
			if (defLaneCapacity > MIN_CAPACITY && defLaneCapacity < MAX_CAPACITY)
				DefaultCapacity = defLaneCapacity;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid max capacity value.");

			if (maxLanesCount > 0 || maxLanesCount <= MAX_COUNT)
				MaxLanesCount = maxLanesCount;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid lane notNullsCount.");

			if (maxTotalBytes > MIN_CAPACITY)
				MaxTotalAllocatedBytes = maxTotalBytes;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid total bytes value.");

			NextCapacity = (i) => DefaultCapacity;
			Disposal = dm;
		}

		/// <summary>
		/// A function handler which will be invoked if the MaxLanesCount threshold is reached.
		/// The MemoryCarriage would expect a boolean response indicating whether to continue
		/// with the allocation or to throw an exception (default behavior).
		/// </summary>
		/// <exception cref="MemoryLaneException">Code.MaxLanesCountReached</exception>
		public Func<bool> OnMaxLaneReached;

		/// <summary>
		/// A handler for the case of allocating more than MaxTotalAllocatedBytes in all lanes.
		/// If there is a function and it returns true the MemoryCarriage will continue as if 
		/// there is no limit, otherwise throws.
		/// </summary>
		/// <exception cref="MemoryLaneException">Code.MaxTotalAllocBytesReached</exception>
		public Func<bool> OnMaxTotalBytesReached;

		/// <summary>
		/// If set, the function may specify different than the default capacity based on 
		/// the current number of lanes. By default always returns the DefaultCapacity value.
		/// </summary>
		public Func<int, int> NextCapacity;

		public const int MAX_COUNT = 5000;
		public const int MIN_CAPACITY = 1023;
		public const int MAX_CAPACITY = 2_000_000_000;

		/// <summary>
		/// Controls how many full cycles around all lanes should be made and fail to enter the 
		/// lock with the specified awaitMS before creating a new lane.
		/// Default value = 10.
		/// </summary>
		public int NoWaitLapsBeforeNewLane = 10;

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
		public readonly MemoryLane.DisposalMode Disposal;
	}
}