/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace System.Collections.Concurrent
{
	/// <summary>
	/// For structs wrapping. 
	/// </summary>
	/// <typeparam name="T">A struct</typeparam>
	public class TesseractCell<T> : IComparable<TesseractCell<T>> where T : struct, IComparable<T>
	{
		public TesseractCell() { }
		public TesseractCell(T v) { Value = v; }

		public readonly T Value;

		public static implicit operator T(TesseractCell<T> c) => c != null ? c.Value : default(T);
		public static implicit operator TesseractCell<T>(T v) => new TesseractCell<T>(v);
		public int CompareTo(TesseractCell<T> other) => Value.CompareTo(other.Value);
		public override bool Equals(object obj) => this.Equals(obj as TesseractCell<T>);
		public override int GetHashCode() => HashCode.Combine(Value);

		public bool Equals(TesseractCell<T> c)
		{
			if (Object.ReferenceEquals(c, null)) return false;
			if (Object.ReferenceEquals(this, c)) return true;
			if (this.GetType() != c.GetType()) return false;

			return c.Value.Equals(Value);
		}
	}

	class TesseractKeyCell<K, V>
	{
		public TesseractKeyCell() { }
		public TesseractKeyCell(K key, V value) { Key = key; Value = value; }
		public TesseractKeyCell<K, V> Clone(in V value) => new TesseractKeyCell<K, V>(Key, value);

		public readonly K Key;
		public readonly V Value;
	}

	/// <summary>
	/// An Expansion calculator for the Tesseract.
	/// An instance is called whenever more blocks are needed.
	/// One could expand differently, depending on the current array size.
	/// </summary>
	/// <param name="allocatedSlots">The current AllocationSlots value.</param>
	/// <returns>The desired new AllocatedSlots count.</returns>
	public delegate int TesseractExpansion(int allocatedSlots);

	/// <summary>
	/// The allowed concurrent operations.
	/// </summary>
	public enum TesseractGear
	{
		/// <summary>
		/// Concurrent gets, sets, Take(), expand Resize() and NotNullItems() are enabled,
		/// but not Append/Remove or shrink Resize.
		/// </summary>
		N = 0,
		/// <summary>
		/// Concurrent Append(), Take(), gets and sets, expand Resize() and NotNullItems() are enabled.
		/// MoveAppendIndex() is not allowed.
		/// </summary>
		Straight = 1,
		/// <summary>
		/// Concurrent Take(), RemoveLast() gets, sets and expand Resize() are enabled.
		/// MoveAppendIndex() is not allowed.
		/// </summary>
		/// <remarks>
		/// Reading is allowed if less than AlloatedSlots, Writes must be less than AppendIndex.
		/// </remarks>
		Reverse = -1,
		/// <summary>
		/// Shrink Resize() is allowed.
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
