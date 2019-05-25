/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
		public MappedHighway() : this(DEF_MMF_LANES) { }

		/// <summary>
		/// Creates new lanes with the specified lengths and a default HighwaySettings instance.
		/// Note that every lane is one memory mapped file.
		/// </summary>
		/// <param name="lanes">The initial layout.</param>
		public MappedHighway(params int[] lanes)
			: base(new HighwaySettings()) => Create(lanes);

		/// <summary>
		/// Creates new lanes with the specified lengths and settings.
		/// When needed, the MemoryCarriage will create the new lanes with settings.DefaultCapacity in length.
		/// Note that every lane is a separate memory mapped file.
		/// </summary>
		/// <param name="stg">Generic settings for all MemoryCarriage derivatives.</param>
		/// <param name="lanes">The initial setup.</param>
		public MappedHighway(HighwaySettings stg, params int[] lanes)
			: base(stg) => Create(lanes);

		public MappedHighway(HighwaySettings stg)
			: base(stg) => Create(DEF_MMF_LANES);

		public override StorageType Type => StorageType.MemoryMappedFileLane;

		protected override MappedFragment createFragment(MappedLane ml, int size, int tries, int awaitMS) =>
			ml.AllocMappedFragment(size, tries, awaitMS);

		protected override MappedLane createLane(int size) => new MappedLane(size, null);

		/// <summary>
		/// Update before calling the default ctor.
		/// </summary>
		public static int[] DEF_MMF_LANES = new int[] { 8_000_000, 4_000_000 };
	}
}
