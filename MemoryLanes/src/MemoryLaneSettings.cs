
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

		public Func<bool> OnMaxLaneReached;
		public Func<bool> OnMaxTotalBytesReached;

		public const int MAX_COUNT = 200;
		public const int MIN_CAPACITY = 1024;
		public const int MAX_CAPACITY = 2_000_000_000;
		public readonly int DefaultCapacity;
		public readonly int MaxLanesCount;
		public readonly long MaxTotalAllocatedBytes;
	}
}