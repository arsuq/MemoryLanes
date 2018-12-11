using System.Collections.Generic;
using System.Threading;

namespace System
{
	public interface IHighwayAlloc : IDisposable
	{
		MemoryFragment AllocFragment(int size);
		int GetTotalActiveFragments();
		int GetTotalCapacity();
		int GetLanesCount();
	}

	public delegate bool FragmentCtor<L, F>(L ml, int size, ref F f) where L : MemoryLane where F : MemoryFragment, new();
	public delegate L LaneCtor<L>(int size) where L : MemoryLane;

	/// <summary>
	/// The allocation/release behavior is generalized here.
	/// </summary>
	/// <typeparam name="L">A Lane</typeparam>
	/// <typeparam name="F">The corresponding fragment type</typeparam>
	public class MemoryCarriage<L, F> : IHighwayAlloc, IDisposable where L : MemoryLane where F : MemoryFragment, new()
	{
		public MemoryCarriage(FragmentCtor<L, F> fc, LaneCtor<L> lc, MemoryLaneSettings stg)
		{
			if (stg == null || fc == null || lc == null) throw new ArgumentNullException();

			settings = stg;
			fragCtor = fc;
			laneCtor = lc;

			AppDomain.CurrentDomain.ProcessExit += (s, e) => destroy();
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
		/// In both cases if the callbacks return true the MemoryCarriage will continue to allocate lanes. 
		/// Code.SizeOutOfRange: when at least one of the lengths is outside the 
		/// MemoryLaneSettings.MIN_CAPACITY - MemoryLaneSettings.MAX_CAPACITY interval.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If count is outside the 1-MemoryLaneSettings.MAX_COUNT interval </exception>
		public void Create(int count)
		{
			if (count < 1 || count > MemoryLaneSettings.MAX_COUNT) throw new ArgumentOutOfRangeException("count");

			for (int i = 0; i < count; i++)
				CreateLane(settings.DefaultCapacity);
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
		/// In both cases if the callbacks return true the MemoryCarriage will continue to allocate lanes. 
		/// Code.SizeOutOfRange: when at least one of the lengths is outside the 
		/// MemoryLaneSettings.MIN_CAPACITY - MemoryLaneSettings.MAX_CAPACITY interval.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">When the laneSizes is either null or has zero items.</exception>
		public void Create(int[] laneSizes)
		{
			if (laneSizes == null || laneSizes.Length < 1) throw new ArgumentNullException("laneSizes");

			foreach (var ls in laneSizes)
				CreateLane(ls);
		}

		/// <summary>
		/// Allocates a generic fragment with a specified length.
		/// </summary>
		/// <param name="size">The number of bytes to allocate.</param>
		/// <returns>The fragment structure.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// If size is negative or greater than MemoryLaneSettings.MAX_CAPACITY.
		/// </exception>
		/// <exception cref="System.MemoryLaneException">
		/// Code.NotInitialized: when the lanes are not initialized.
		/// Code.NewLaneAllocFail: after an unsuccessful attempt to allocate a fragment in a dedicated new lane.
		/// One should never see this one!
		/// </exception>
		public F Alloc(int size)
		{
			if (Lanes == null || Lanes.Count < 1) throw new MemoryLaneException(MemoryLaneException.Code.NotInitialized);
			if (size < 0 || size > MemoryLaneSettings.MAX_CAPACITY) throw new ArgumentOutOfRangeException("size");

			var frag = new F();

			// Start from the oldest lane
			for (var i = 0; i < Lanes.Count; i++)
				if (fragCtor(Lanes[i], size, ref frag))
					return frag;

			// No luck, create a new lane
			var cap = size > settings.DefaultCapacity ? size : settings.DefaultCapacity;
			var ml = CreateLane(cap);

			if (!fragCtor(ml, size, ref frag))
				throw new MemoryLaneException(
					MemoryLaneException.Code.NewLaneAllocFail,
					string.Format("Failed to allocate {0} bytes on a dedicated lane.", size));

			Interlocked.Exchange(ref lastAnyLaneAllocTick, DateTime.Now.Ticks);

			return frag;
		}

		public MemoryFragment AllocFragment(int size) => Alloc(size);

		public void Dispose() => destroy();

		/// <summary>
		/// Returns an aggregate of all active fragments in all lanes.
		/// </summary>
		/// <returns>The number of active fragments</returns>
		public int GetTotalActiveFragments()
		{
			// It's fine not to lock because the lanes could only increase
			var lc = Lanes.Count;
			var c = 0;
			for (int i = 0; i < lc; i++) c += Lanes[i].Allocations;
			return c;
		}

		/// <summary>
		/// Sums the lengths of all lanes.
		/// </summary>
		/// <returns>The total preallocated space for the highway.</returns>
		public int GetTotalCapacity()
		{
			var lc = Lanes.Count;
			var cap = 0;
			for (int i = 0; i < lc; i++) cap += Lanes[i].LaneCapacity;
			return cap;
		}

		/// <summary>
		/// Gets the Lanes count.
		/// </summary>
		/// <returns>The number of preallocated lanes.</returns>
		public int GetLanesCount() => Lanes.Count;

		L CreateLane(int capacity)
		{
			var ignore = false;
			var lanesTotalLength = 0;

			if (Lanes.Count + 1 >= settings.MaxLanesCount)
			{
				if (settings.OnMaxLaneReached != null) ignore = settings.OnMaxLaneReached();
				if (!ignore) throw new MemoryLaneException(MemoryLaneException.Code.MaxLanesCountReached);
			}

			foreach (var l in Lanes) lanesTotalLength += l.LaneCapacity;

			if (lanesTotalLength > settings.MaxTotalAllocatedBytes)
			{
				ignore = false;
				if (settings.OnMaxTotalBytesReached != null) ignore = settings.OnMaxTotalBytesReached();
				if (!ignore) throw new MemoryLaneException(MemoryLaneException.Code.MaxTotalAllocBytesReached);
			}

			var ml = laneCtor(capacity);
			Lanes.Add(ml);

			return ml;
		}

		void destroy(bool isGC = false)
		{
			if (!isDisposed)
			{
				try
				{
					if (Lanes != null && Lanes.Count > 0)
						foreach (var lane in Lanes)
							lane.Dispose();
				}
				catch { }
				if (isGC) GC.SuppressFinalize(this);
				isDisposed = true;
			}
		}

		~MemoryCarriage() => destroy(true);

		protected readonly MemoryLaneSettings settings;
		/// <summary>
		/// Use to detect bad disposal behavior. 
		/// </summary>
		public long LastAnyLaneAllocTick => Thread.VolatileRead(ref lastAnyLaneAllocTick);
		long lastAnyLaneAllocTick;

		LaneCtor<L> laneCtor;
		FragmentCtor<L, F> fragCtor;
		List<L> Lanes = new List<L>();
		bool isDisposed;
	}
}
