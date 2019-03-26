/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
		/// Creates new lanes with the specified lengths and a default HighwaySettings instance.
		/// </summary>
		/// <param name="lanes">The initial layout.</param>
		public MarshalHighway(params int[] lanes)
			: base(new HighwaySettings()) => Create(lanes);

		/// <summary>
		/// Creates new lanes with the specified lengths and settings.
		/// When needed, the MemoryCarriage will create the new lanes with settings.DefaultCapacity in length.
		/// </summary>
		/// <param name="stg">Generic settings for all MemoryCarriage derivatives.</param>
		/// <param name="lanes">The initial setup.</param>
		public MarshalHighway(HighwaySettings stg, params int[] lanes)
			: base(stg) => Create(lanes);

		public MarshalHighway(HighwaySettings stg)
			: base(stg) => Create(DEF_NHEAP_LANES);

		public override StorageType Type => StorageType.NativeHeapLane;

		protected override MarshalLaneFragment createFragment(MarshalLane ml, int size, int tries) =>
			ml.AllocMarshalFragment(size, tries);

		protected override MarshalLane createLane(int size) => new MarshalLane(size, settings.Disposal);

		/// <summary>
		/// Update before calling the default ctor.
		/// </summary>
		public static int[] DEF_NHEAP_LANES = new int[] { 8_000_000, 4_000_000 };
	}
}
