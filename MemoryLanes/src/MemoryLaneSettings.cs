
namespace System
{
	public class MemoryLaneSettings
	{
		public MemoryLaneSettings(
			int defLaneCapacity = 8_000_000,
			int maxLanesCount = MAX_COUNT,
			long maxTotalBytes = 2_000_000_000)
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
				"Invalid lane count.");

			if (maxTotalBytes > MIN_CAPACITY)
				MaxTotalAllocatedBytes = maxTotalBytes;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid total bytes value.");
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

		public const int MAX_COUNT = 5000;
		public const int MIN_CAPACITY = 1023;
		public const int MAX_CAPACITY = 2_000_000_000;

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
	}
}