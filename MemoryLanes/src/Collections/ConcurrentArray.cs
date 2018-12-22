using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Collections.Concurrent
{
	/// <summary>
	/// Represents an ordered list of fix-sized arrays as one virtual contiguous array. 
	/// </summary>
	/// <remarks>
	/// Growing allocates as much new arrays as configured, each with the same BaseLength.
	/// The synchronization mechanism is ReaderWriterLockSlim such that all methods 
	/// except Append() and Resize() acquire read locks.
	/// </remarks>
	/// <typeparam name="T">A class.</typeparam>
	public class ConcurrentArray<T>  where T : class
	{
		public ConcurrentArray(int baseLength, int count = 1, Func<int, int> expansionLen = null)
		{
			if (baseLength < 1) throw new ArgumentException("BaseLength");
			if (count < 1) throw new ArgumentException("notNullsCount");
			if (expansionLen != null) ExpansionLength = expansionLen;

			this.BaseLength = baseLength;
			for (int i = 0; i < count; i++)
			{
				blocks.Add(new T[baseLength]);
				combinedLength += baseLength;
			}
		}

		/// <summary>
		/// The sequence incremental size.
		/// </summary>
		public readonly int BaseLength;

		/// <summary>
		/// The biggest Add or set offset. 
		/// </summary>
		public int AppendPos => Volatile.Read(ref appendPos);

		/// <summary>
		/// Counts the non null items in the sequence.
		/// </summary>
		public int Count => Volatile.Read(ref notNullsCount);

		/// <summary>
		/// The array sequence length
		/// </summary>
		public int Capacity => Volatile.Read(ref combinedLength);

		/// <summary>
		/// Will be called before allocating more space, providing the current length as an argument.
		/// The returned value is desired new total length.
		/// The default growth factor is 1.8.
		/// </summary>
		/// <remarks>
		/// Use this callback to fine tune the amount of blocked memory and/or the allocation time.
		/// By providing different growth factors depending on the current size,
		/// one can create a best fitting allocation/capacity curve.
		/// </remarks>
		Func<int, int> ExpansionLength = (c) => Convert.ToInt32(c * 1.8);

		/// <summary>
		/// Access to the individual cells.
		/// </summary>
		/// <param name="index">Must be less than AppendPos</param>
		/// <returns>The object reference at the index</returns>
		/// <exception cref="ArgumentOutOfRangeException">index</exception>
		public T this[int index]
		{
			get
			{
				if (index < 0 || index > AppendPos)
					throw new ArgumentOutOfRangeException("index");

				gate.EnterReadLock();

				// Could be resized while waiting
				int seq = -1, idx = -1;
				Index2Seq(index, ref seq, ref idx);

				var r = Volatile.Read(ref blocks[seq][idx]);

				gate.ExitReadLock();

				return r;
			}
			set
			{
				if (index < 0 || index > AppendPos)
					throw new ArgumentOutOfRangeException("index");

				// Still it's a reader lock because there is no resizing here
				gate.EnterReadLock();

				int seq = -1, idx = -1;
				Index2Seq(index, ref seq, ref idx);

				var original = Interlocked.Exchange(ref blocks[seq][idx], value);
				if (value != null) Interlocked.Increment(ref notNullsCount);
				else if (original != null) Interlocked.Decrement(ref notNullsCount);

				gate.ExitReadLock();
			}
		}

		/// <summary>
		/// Appends an item after the AppendPos index.
		/// </summary>
		/// <param name="item">The object reference</param>
		/// <returns>The index of the item</returns>
		public int Append(T item)
		{
			var pos = -1;

			// Do not throw ArgumentNull
			if (item == null) return pos;

			gate.EnterUpgradeableReadLock();

			try
			{
				pos = Interlocked.Increment(ref appendPos);

				if (pos >= Capacity)
				{
					gate.EnterWriteLock();

					try
					{
						// Could have been augmented by another thread during the wait.
						// Add as much baseLenght tiles as needed.
						var cap = Capacity; // Volatile read once
						if (pos >= cap)
						{
							var newCap = ExpansionLength(cap);
							while (newCap > combinedLength)
							{
								blocks.Add(new T[BaseLength]);
								Interlocked.Add(ref combinedLength, BaseLength);
							}
						}
					}
					finally
					{
						if (gate.IsWriteLockHeld) gate.ExitWriteLock();
					}
				}

				// Set the value
				int seq = -1, idx = -1;
				Index2Seq(pos, ref seq, ref idx);
				Interlocked.Exchange<T>(ref blocks[seq][idx], item);
				Interlocked.Increment(ref notNullsCount);
			}
			finally
			{
				if (gate.IsUpgradeableReadLockHeld) gate.ExitUpgradeableReadLock();
			}

			return pos;
		}

		/// <summary>
		/// Decrements the AppendPos, nulls the corresponding cell and returns the item that was there.
		/// </summary>
		/// <param name="pos">The item position.</param>
		/// <returns>The removed item.</returns>
		/// <exception cref="System.IndexOutOfRangeException">When there are no items.</exception>
		public T RemoveLast(out int pos)
		{
			gate.EnterReadLock();

			pos = AppendPos;

			if (pos >= 0)
			{
				T theItem = null;
				int seq = -1, idx = -1;

				Index2Seq(pos, ref seq, ref idx);

				theItem = Interlocked.Exchange<T>(ref blocks[seq][idx], null);
				Interlocked.Decrement(ref appendPos);
				Interlocked.Decrement(ref notNullsCount);

				gate.ExitReadLock();
				return theItem;
			}
			else
			{
				gate.ExitReadLock();
				throw new IndexOutOfRangeException("Nothing to remove.");
			}
		}

		/// <summary>
		/// Looks for the item and nulls the array cell, 
		/// this will decrement the Count value.
		/// </summary>
		/// <param name="item">The object reference</param>
		/// <returns>True if found and null-ed</returns>
		public bool Remove(T item)
		{
			if (item == null) return false;

			var idx = IndexOf(item);

			if (idx >= 0)
			{
				this[idx] = null;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Iterates all cells from 0 up to AppendPos and yields the  item if it's not null at the time of the check.
		/// The longLock determines whether the indexer would be used, thus locking on each individual read
		/// or one long lock would be held for the entire enumeration.
		/// </summary>
		/// <param name="longLock">If true (the default) acquires a ReaderLock for the duration of the whole traversal.</param>
		/// <returns>A not null item.</returns>
		public IEnumerable<T> Items(bool longLock = true)
		{
			T item = null;
			var LEN = AppendPos;

			if (longLock)
			{
				gate.EnterReadLock();

				try
				{
					int seq = -1, idx = -1;

					for (int i = 0; i <= LEN; i++)
					{
						Index2Seq(i, ref seq, ref idx);
						item = Volatile.Read(ref blocks[seq][idx]);
						if (item != null)
							yield return item;
					}
				}
				finally
				{
					if (gate.IsReadLockHeld) gate.ExitReadLock();
				}
			}
			else
				for (int i = 0; i <= LEN; i++)
				{
					item = this[i];
					if (item != null)
						yield return item;
				}
		}

		/// <summary>
		/// Searches for an item by traversing all cells up to AppendPos, which is 
		/// the furthest index in the array.
		/// Acquires a ReaderLock.
		/// </summary>
		/// <param name="item">The object ref</param>
		/// <returns>A positive value if the item is found, -1 otherwise.</returns>
		public int IndexOf(T item)
		{
			int seq = -1, idx = -1, result = -1;

			gate.EnterReadLock();

			for (var i = 0; i <= AppendPos; i++)
			{
				Index2Seq(i, ref seq, ref idx);
				if (Volatile.Read(ref blocks[seq][idx]) == item)
				{
					result = i;
					break;
				}
			}

			gate.ExitReadLock();

			return result;
		}


		/// <summary>
		/// Expands or shrinks the virtual array to the number of base-length tiles fitting the requested length.
		/// If the AppendPos is greater that the new length, it's cut to length -1.
		/// If shrinking, the number of not-null values, i.e. Count is also updated.
		/// </summary>
		/// <param name="length">The new length.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">If length is negative</exception>
		public void Resize(int length)
		{
			if (length < 0) throw new ArgumentOutOfRangeException("length");
			if (length == Capacity) return;

			gate.EnterWriteLock();

			try
			{
				if (length > Capacity)
				{
					while (length > combinedLength)
					{
						blocks.Add(new T[BaseLength]);
						Interlocked.Add(ref combinedLength, BaseLength);
					}
				}
				else
				{
					while (length < combinedLength)
					{
						var last = blocks.Count - 1;
						blocks.Remove(blocks[last]);
						Interlocked.Add(ref combinedLength, -BaseLength);
					}

					Interlocked.Exchange(ref appendPos, length - 1);

					int notNull = 0;
					foreach (var tile in blocks)
						foreach (var cell in tile)
							if (cell != null) notNull++;

					Interlocked.Exchange(ref notNullsCount, notNull);
				}
			}
			finally
			{
				if (gate.IsWriteLockHeld) gate.ExitWriteLock();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Index2Seq(int index, ref int seq, ref int idx)
		{
			if (index < BaseLength)
			{
				seq = 0;
				idx = index;
			}
			else
			{
				seq = index / BaseLength;
				idx = index % BaseLength;
			}
		}

		int combinedLength;
		int appendPos = -1;
		int notNullsCount;

		ReaderWriterLockSlim gate = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
		List<T[]> blocks = new List<T[]>();
	}
}
