using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace System
{
	public interface IHighway : IDisposable
	{
		MemoryFragment AllocFragment(int size, int awaitMS = -1);
		int GetTotalActiveFragments();
		int GetTotalCapacity();
		int GetLanesCount();
		long LastAllocTickAnyLane { get; }
		IReadOnlyList<MemoryLane> GetLanes();
		MemoryLane this[int index] { get; }
	}

	public delegate bool FragmentCtor<L, F>(L ml, int size, ref F f, int awaitMS) where L : MemoryLane where F : MemoryFragment, new();
	public delegate L LaneCtor<L>(int size) where L : MemoryLane;

	/// <summary>
	/// The allocation/release behavior is generalized here.
	/// </summary>
	/// <typeparam name="L">A Lane</typeparam>
	/// <typeparam name="F">The corresponding fragment type</typeparam>
	public class MemoryCarriage<L, F> : IHighway, IDisposable where L : MemoryLane where F : MemoryFragment, new()
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
		public void Create(params int[] laneSizes)
		{
			if (laneSizes == null || laneSizes.Length < 1) throw new ArgumentNullException("laneSizes");

			foreach (var ls in laneSizes)
				CreateLane(ls);
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
			if (Lanes == null || Lanes.Count < 1) throw new MemoryLaneException(MemoryLaneException.Code.NotInitialized);
			if (size < 0 || size > MemoryLaneSettings.MAX_CAPACITY) throw new ArgumentOutOfRangeException("size");

			var frag = new F();

			// Start from the oldest lane
			// If awaitMS > -1 cycle around a few times before making a new lane
			var LAPS = awaitMS > -1 ? settings.NoWaitLapsBeforeNewLane : 1;
			for (var laps = 0; laps < LAPS; laps++)
				for (var i = 0; i <= Lanes.AppendPos; i++)
				{
					var lane = Lanes[i];
					if (lane != null && fragCtor(lane, size, ref frag, awaitMS))
						return frag;
				}

			// No luck, create a new lane and do not publish it before getting a fragment
			var nextCapacity = settings.NextCapacity(Lanes.Count);
			var cap = size > nextCapacity ? size : nextCapacity;
			var ml = CreateLane(cap, true);

			if (!fragCtor(ml, size, ref frag, awaitMS))
				throw new MemoryLaneException(
					MemoryLaneException.Code.NewLaneAllocFail,
					string.Format("Failed to allocate {0} bytes on a dedicated lane.", size));

			Lanes.Append(ml);

			Interlocked.Exchange(ref lastAllocTickAnyLane, DateTime.Now.Ticks);

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

		/// <summary>
		/// Creates a new List instance with the selection of all non null cells in the underlying array.
		/// This is a relatively expensive operation, depending on the array length and the AppendPos value, so
		/// you may consider using the indexer instead.
		/// </summary>
		/// <returns>A read only list of MemoryLane objects.</returns>
		public IReadOnlyList<MemoryLane> GetLanes() => new List<MemoryLane>(Lanes.Items());

		/// <summary>
		/// Get a specific lane.
		/// Use this method instead of GetLanes() which 
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public MemoryLane this[int index]
		{
			get
			{
				if (index < 0 || index > Lanes.Capacity) throw new ArgumentOutOfRangeException("index");
				return Lanes[index];
			}
		}

		L CreateLane(int capacity, bool hidden = false)
		{
			var ignore = false;
			var lanesTotalLength = 0;

			if (Lanes.Count + 1 > settings.MaxLanesCount)
			{
				if (settings.OnMaxLaneReached != null) ignore = settings.OnMaxLaneReached();
				if (!ignore) throw new MemoryLaneException(MemoryLaneException.Code.MaxLanesCountReached);
			}

			foreach (var l in Lanes.Items())
				lanesTotalLength += l.LaneCapacity;

			if (lanesTotalLength > settings.MaxTotalAllocatedBytes)
			{
				ignore = false;
				if (settings.OnMaxTotalBytesReached != null) ignore = settings.OnMaxTotalBytesReached();
				if (!ignore) throw new MemoryLaneException(MemoryLaneException.Code.MaxTotalAllocBytesReached);
			}

			var ml = laneCtor(capacity);

			if (!hidden) Lanes.Append(ml);

			return ml;
		}

		void destroy(bool isGC = false)
		{
			if (!isDisposed)
			{
				try
				{
					if (Lanes != null && Lanes.Count > 0)
						foreach (var lane in Lanes.Items())
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
		public long LastAllocTickAnyLane => Thread.VolatileRead(ref lastAllocTickAnyLane);
		long lastAllocTickAnyLane;

		LaneCtor<L> laneCtor;
		FragmentCtor<L, F> fragCtor;
		ConcurrentFixedArray<L> Lanes = new ConcurrentFixedArray<L>(MemoryLaneSettings.MAX_COUNT);
		bool isDisposed;
	}
}
