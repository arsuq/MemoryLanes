/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace System.Collections.Concurrent
{
	/// <summary>
	/// An expansion calculator for the Tesseract.
	/// An instance is called whenever more blocks are needed.
	/// One could expand differently, depending on the current array size.
	/// </summary>
	/// <param name="allocatedSlots">The current AllocationSlots value.</param>
	/// <returns>The desired new AllocatedSlots count.</returns>
	public delegate int TesseractExpansion(in int allocatedSlots);

	/// <summary>
	/// The allowed concurrent operations.
	/// </summary>
	public enum TesseractGear
	{
		/// <summary>
		/// Concurrent gets, sets and Take() are enabled, but not Append/Remove or Resize.
		/// </summary>
		N = 0,
		/// <summary>
		/// Concurrent Append(), Take(), gets and sets are enabled.
		/// </summary>
		Straight = 1,
		/// <summary>
		/// Concurrent Take(), RemoveLast() gets and sets are enabled.
		/// </summary>
		/// <remarks>
		/// Reading is allowed if less than AlloatedSlots, Writes must be less than AppendIndex.
		/// </remarks>
		Reverse = -1,
		/// <summary>
		/// Only Resize() is allowed.
		/// </summary>
		P = -2
	}

	struct TesseractPos
	{
		public TesseractPos(int index)
		{
			D0 = D1 = D2 = D3 = 0;
			Set(index);
		}

		public void Set(int index)
		{
			unsafe
			{
				byte* p = (byte*)&index;

				if (BitConverter.IsLittleEndian)
				{
					D0 = p[3];
					D1 = p[2];
					D2 = p[1];
					D3 = p[0];
				}
				else
				{
					D0 = p[0];
					D1 = p[1];
					D2 = p[2];
					D3 = p[3];
				}
			}
		}

		public byte D0;
		public byte D1;
		public byte D2;
		public byte D3;
	}
}
