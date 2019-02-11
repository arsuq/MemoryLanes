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
		/// Creates new lanes with the specified lengths and a default MemoryLaneSettings instance.
		/// Note that every lane is one memory mapped file.
		/// </summary>
		/// <param name="lanes">The initial layout.</param>
		public MappedHighway(params int[] lanes)
			: base(new MemoryLaneSettings()) => Create(lanes);

		/// <summary>
		/// Creates new lanes with the specified lengths and settings.
		/// When needed, the MemoryCarriage will create the new lanes with settings.DefaultCapacity in length.
		/// Note that every lane is a separate memory mapped file.
		/// </summary>
		/// <param name="stg">Generic settings for all MemoryCarriage derivatives.</param>
		/// <param name="lanes">The initial setup.</param>
		public MappedHighway(MemoryLaneSettings stg, params int[] lanes)
			: base(stg) => Create(lanes);

		public MappedHighway(MemoryLaneSettings stg)
			: base(stg) => Create(DEF_MMF_LANES);

		public override HighwayType Type => HighwayType.Mapped;

		protected override bool createFragment(MappedLane ml, int size, ref MappedFragment f, int awaitMS) =>
			ml.TryCreateFragment(size, ref f, awaitMS);

		protected override MappedLane createLane(int size) => new MappedLane(size, null, settings.Disposal);

		static int[] DEF_MMF_LANES = new int[] { 8_000_000, 4_000_000 };
	}
}
