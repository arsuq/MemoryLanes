namespace System
{
	/// <summary>
	/// Allocates memory lanes via Marshal.AllocHGlobal
	/// </summary>
	public class MarshalHighway : MemoryCarriage<MarshalLane, MarshalLaneFragment>
	{
		/// <summary>
		/// Creates a 2 lane highway with lengths 8MB and 4MB
		/// </summary>
		public MarshalHighway() : this(DEF_NHEAP_LANES) { }

		/// <summary>
		/// Creates new lanes with the specified lengths and a default MemoryLaneSettings instance.
		/// </summary>
		/// <param name="lanes">The initial layout.</param>
		public MarshalHighway(params int[] lanes)
			: base(new MemoryLaneSettings()) => Create(lanes);

		/// <summary>
		/// Creates new lanes with the specified lengths and settings.
		/// When needed, the MemoryCarriage will create the new lanes with settings.DefaultCapacity in length.
		/// </summary>
		/// <param name="stg">Generic settings for all MemoryCarriage derivatives.</param>
		/// <param name="lanes">The initial setup.</param>
		public MarshalHighway(MemoryLaneSettings stg, params int[] lanes)
			: base(stg) => Create(lanes);

		public MarshalHighway(MemoryLaneSettings stg)
			: base(stg) => Create(DEF_NHEAP_LANES);

		protected override bool createFragment(MarshalLane ml, int size, ref MarshalLaneFragment f, int awaitMS) =>
			ml.TryCreateFragment(size, ref f, awaitMS);

		protected override MarshalLane createLane(int size) => new MarshalLane(size);

		static int[] DEF_NHEAP_LANES = new int[] { 8_000_000, 4_000_000 };
	}
}
