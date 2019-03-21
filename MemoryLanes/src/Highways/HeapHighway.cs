/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace System
{
	/// <summary>
	/// Allocates memory lanes on the large object heap (if the length is > 80K).
	/// </summary>
	public class HeapHighway : MemoryCarriage<HeapLane, HeapFragment>
	{
		/// <summary>
		/// Creates a 2 lane highway with lengths 8MB and 4MB
		/// </summary>
		public HeapHighway() : this(DEF_HEAP_LANES) { }

		/// <summary>
		/// Creates new lanes with the specified lengths and a default HighwaySettings instance.
		/// </summary>
		/// <param name="lanes">The initial layout.</param>
		public HeapHighway(params int[] lanes)
			: base(new HighwaySettings()) => Create(lanes);

		/// <summary>
		/// Creates new lanes with the specified lengths and settings.
		/// When needed, the MemoryCarriage will create the new lanes with settings.DefaultCapacity in length.
		/// </summary>
		/// <param name="stg">Generic settings for all MemoryCarriage derivatives.</param>
		/// <param name="lanes">The initial setup.</param>
		public HeapHighway(HighwaySettings stg, params int[] lanes)
			: base(stg) => Create(lanes);


		public HeapHighway(HighwaySettings stg)
			: base(stg) => Create(DEF_HEAP_LANES);

		public override StorageType Type => StorageType.ManagedHeapLane;

		protected override HeapFragment createFragment(HeapLane ml, int size, int tries) =>
			ml.AllocHeapFragment(size, tries);

		protected override HeapLane createLane(int size) => new HeapLane(size, settings.Disposal);

		static int[] DEF_HEAP_LANES = new int[] { 8_000_000, 4_000_000 };
	}
}
