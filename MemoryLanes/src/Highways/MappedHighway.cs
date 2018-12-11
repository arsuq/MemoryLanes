namespace System
{
	/// <summary>
	/// Allocates memory lanes as memory mapped files - one lane is one file.
	/// </summary>
	public class MappedHighway : MemoryCarriage<MappedLane, MappedFragment>
	{
		/// <summary>
		/// Creates a 2 lane highway with lengths 8MB and 4MB
		/// </summary>
		public MappedHighway() : this(8_000_000, 4_000_000) { }

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

		static bool FragMaker(MappedLane lane, int size, ref MappedFragment frag) => lane.TryCreateFragment(size, ref frag);
		static MappedLane LaneMaker(int size) => new MappedLane(size);
	}
}
