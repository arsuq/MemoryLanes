namespace System
{
	/// <summary>
	/// Allocates memory lanes via Marshal.AllocHGlobal
	/// </summary>
	public class MarshalHighway : MemoryCarriage<MarshalLane, MarshalFragment>
	{
		/// <summary>
		/// Creates new lanes with the specified lengths and a default MemoryLaneSettings instance.
		/// </summary>
		/// <param name="lanes">The initial layout.</param>
		public MarshalHighway(params int[] lanes)
			: base(FragMaker, LaneMaker, new MemoryLaneSettings()) => Create(lanes);

		/// <summary>
		/// Creates new lanes with the specified lengths and settings.
		/// When needed, the MemoryCarriage will create the new lanes with settings.DefaultCapacity in length.
		/// </summary>
		/// <param name="stg">Generic settings for all MemoryCarriage derivatives.</param>
		/// <param name="lanes">The initial setup.</param>
		public MarshalHighway(MemoryLaneSettings stg, params int[] lanes)
			: base(FragMaker, LaneMaker, stg) => Create(lanes);

		static bool FragMaker(MarshalLane lane, int size, ref MarshalFragment frag) => lane.TryCreateFragment(size, ref frag);
		static MarshalLane LaneMaker(int size) => new MarshalLane(size);
	}
}
