namespace System
{
	/// <summary>
	/// Allocates memory lanes as memory mapped files - one lane is one file.
	/// </summary>
	public class MappedHighway : MemoryCarriage<MMFLane, MMFFragment>
	{
		/// <summary>
		/// Creates new lanes with the specified lengths and a default MemoryLaneSettings instance.
		/// Note that every lane is one memory mapped file.
		/// </summary>
		/// <param name="lanes">The initial layout.</param>
		public MappedHighway(params int[] lanes)
			: base(FragMaker, LaneMaker, new MemoryLaneSettings()) => Create(lanes);

		/// <summary>
		/// Creates new lanes with the specified lengths and settings.
		/// When needed, the MemoryCarriage will create the new lanes with settings.DefaultCapacity in length.
		/// Note that every lane is a separate memory mapped file.
		/// </summary>
		/// <param name="stg">Generic settings for all MemoryCarriage derivatives.</param>
		/// <param name="lanes">The initial setup.</param>
		public MappedHighway(MemoryLaneSettings stg, params int[] lanes)
			: base(FragMaker, LaneMaker, stg) => Create(lanes);

		static bool FragMaker(MMFLane lane, int size, ref MMFFragment frag) => lane.TryCreateFragment(size, ref frag);
		static MMFLane LaneMaker(int size) => new MMFLane(size);
	}
}
