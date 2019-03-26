using System.Collections.Generic;

namespace System.Collections.Concurrent
{
	public class Tesseract<K, V>
	{
		public Tesseract(TesseractPrime hashPrime, V NULL = default(V), byte collisionLine = 16)
			: this((int)hashPrime, collisionLine, 1, null, NULL) { }

		public Tesseract(int hashPrime, V NULL = default(V), byte collisionLine = 16)
			: this((int)hashPrime, collisionLine, 1, null, NULL) { }

		public Tesseract(
			int hashPrime,
			byte collisionLine = 16,
			ushort initTiles = 1,
			TesseractExpansion exp = null,
			V NULL = default(V),
			bool countItems = false)
		{
			if (hashPrime < 0) throw new ArgumentOutOfRangeException("hashPrime");
			if (collisionLine < 1) throw new ArgumentOutOfRangeException("collisionLine");

			COLLISION_LINE = collisionLine;
			HASH_PRIME = hashPrime;
			TILE = hashPrime * COLLISION_LINE;
			CountItems = countItems;
			TSR = new Tesseract<TesseractKeyCell<K, V>>(TILE * initTiles, countItems, exp);
			this.NULL = NULL;

			TSR.Clutch(TesseractGear.N);
		}

		public V this[in K key]
		{
			get => Get(key);
			set => Update(key, value);
		}

		public V Get(in K key)
		{
			var p = (EC.GetHashCode(key) & SIGN_MASK) % HASH_PRIME;

			for (int i = p * COLLISION_LINE; i < TSR.AllocatedSlots; i += TILE)
				for (int c = 0; c < COLLISION_LINE; c++)
				{
					var cell = TSR[i + c];
					if (cell == null) return NULL;
					if (EC.Equals(cell.Key, key)) return cell.Value;
				}

			return NULL;
		}

		public int Set(in K key, in V value, in ushort resizeCount = RESIZE_LIMIT)
		{
			var p = (EC.GetHashCode(key) & SIGN_MASK) % HASH_PRIME;
			var i = p * COLLISION_LINE;
			var v = new TesseractKeyCell<K, V>(key, value);

			for (int rc = 0; rc < resizeCount; rc++)
			{
				for (; i  < TSR.AllocatedSlots - COLLISION_LINE; i += TILE)
					for (int c = 0; c < COLLISION_LINE; c++)
					{
						var idx = i + c;
						var cell = TSR[idx];

						if (cell == null || cell == NULL_CELL || EC.Equals(cell.Key, key))
						{
							TSR[idx] = v;
							return idx;
						}
					}

				var cap = TSR.AllocatedSlots + TSR.DefaultExpansion;

				if (TSR.Expansion != null)
				{
					cap = TSR.Expansion(TSR.AllocatedSlots);
					if (cap <= TSR.AllocatedSlots) break;
				}

				TSR.Resize(cap, true);
			}

			return -1;
		}

		public bool Update(in K key, in V value)
		{
			var p = (EC.GetHashCode(key) & SIGN_MASK) % HASH_PRIME;

			for (int i = p * COLLISION_LINE; i < TSR.AllocatedSlots; i += TILE)
				for (int c = 0; c < COLLISION_LINE; c++)
				{
					var cell = TSR[i + c];
					if (cell == null) return false;
					if (EC.Equals(cell.Key, key))
					{
						TSR[i + c] = cell.Clone(value);
						return true;
					}
				}

			return false;
		}

		public V CAS(in K key, in V value, in V comparand)
		{
			var p = (EC.GetHashCode(key) & SIGN_MASK) % HASH_PRIME;

			for (int i = p * COLLISION_LINE; i < TSR.AllocatedSlots; i += TILE)
				for (int c = 0; c < COLLISION_LINE; c++)
				{
					var idx = i + c;
					var cell = TSR[idx];
					if (cell == null) return NULL;
					if (EC.Equals(cell.Key, key))
					{
						var ncell = cell.Clone(value);
						return TSR.CAS(idx, ncell, cell).Value;
					}
				}

			return NULL;
		}

		public V GetIndex(in int index)
		{
			var cell = TSR[index];
			return cell != null ? cell.Value : NULL;
		}

		public bool UpdateIndex(in int index, in V value)
		{
			var cell = TSR[index];

			if (cell != null)
			{
				TSR[index] = cell.Clone(value);
				return true;
			}

			return false;
		}

		public V CASIndex(in int index, in V value, in V comparand)
		{
			var cell = TSR[index];

			if (cell != null)
			{
				var ncell = cell.Clone(value);
				return TSR.CAS(index, ncell, cell).Value;
			}

			return NULL;
		}

		public int Remove(in K key)
		{
			var p = (EC.GetHashCode(key) & SIGN_MASK) % HASH_PRIME;

			for (int i = p * COLLISION_LINE; i < TSR.AllocatedSlots; i += TILE)
				for (int c = 0; c < COLLISION_LINE; c++)
				{
					var cell = TSR[i + c];
					if (cell == null) return -1;
					if (EC.Equals(cell.Key, key))
					{
						TSR[i + c] = NULL_CELL;
						return i + c;
					}
				}

			return -1;
		}

		public IEnumerable<K> Keys()
		{
			var slots = TSR.AllocatedSlots;

			foreach (var cell in TSR.NotNullItems(false, true))
				yield return cell.Key;
		}

		public IEnumerable<V> Values()
		{
			var slots = TSR.AllocatedSlots;

			foreach (var cell in TSR.NotNullItems())
				yield return cell.Value;
		}

		public void Resize(ushort tiles, bool expand)
		{
			TSR.Clutch(TesseractGear.P);
			TSR.Resize(TILE * tiles, expand);
			TSR.Clutch(TesseractGear.N);
		}

		public int AllocatedSlots => TSR.AllocatedSlots;
		public int ItemsCount => TSR.ItemsCount;

		public readonly int COLLISION_LINE;
		public readonly bool CountItems;

		readonly TesseractKeyCell<K, V> NULL_CELL = new TesseractKeyCell<K, V>();
		readonly IEqualityComparer<K> EC = EqualityComparer<K>.Default;

		const ushort RESIZE_LIMIT = ushort.MaxValue;
		const int SIGN_MASK = 0x7FFF_FFFF;

		readonly V NULL;
		readonly int TILE;
		readonly int HASH_PRIME;
		Tesseract<TesseractKeyCell<K, V>> TSR;
	}
}
