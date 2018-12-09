namespace System
{
	/// <summary>
	/// Allocates memory lanes on the large object heap (if the length is > 80K).
	/// </summary>
	public class HeapHighway : MemoryCarriage<HeapLane, HeapFragment>
	{
		/// <summary>
		/// Creates new lanes with the specified lengths and a default MemoryLaneSettings instance.
		/// </summary>
		/// <param name="lanes">The initial layout.</param>
		public HeapHighway(params int[] lanes)
			: base(FragMaker, LaneMaker, new MemoryLaneSettings()) => Create(lanes);

		/// <summary>
		/// Creates new lanes with the specified lengths and settings.
		/// When needed, the MemoryCarriage will create the new lanes with settings.DefaultCapacity in length.
		/// </summary>
		/// <param name="stg">Generic settings for all MemoryCarriage derivatives.</param>
		/// <param name="lanes">The initial setup.</param>
		public HeapHighway(MemoryLaneSettings stg, params int[] lanes)
			: base(FragMaker, LaneMaker, stg) => Create(lanes);


		static bool FragMaker(HeapLane lane, int size, ref HeapFragment frag) => lane.TryCreateFragment(size, ref frag);
		static HeapLane LaneMaker(int size) => new HeapLane(size);
	}
}
