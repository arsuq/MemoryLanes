/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Collections.Concurrent
{
	/// <summary>
	/// Represents an ordered list of fix-sized arrays as one virtual contiguous array. 
	/// </summary>
	/// <remarks>
	/// Growing allocates as much new arrays as configured, each with the same BaseLength.
	/// </remarks>
	/// <typeparam name="T">A class</typeparam>
	public class ConcurrentArray<T> where T : class
	{
		/// <summary>
		/// The allowed concurrent operations
		/// </summary>
		public enum Gear
		{
			/// <summary>
			/// Concurrent gets and sets are enabled, but not Append/Remove or Resize.
			/// </summary>
			N = 0,
			/// <summary>
			/// Concurrent Append(), gets and sets are enabled.
			/// </summary>
			Straight = 1,
			/// <summary>
			/// Concurrent RemoveLast() gets and sets are enabled.
			/// </summary>
			/// <remarks>
			/// Getting or setting values in this mode is racing with the AppendPos.
			/// </remarks>
			Reverse = -1,
			/// <summary>
			/// Only Resize() is allowed.
			/// </summary>
			P = -2
		}

		/// <summary>
		/// Creates new Concurrent array with predefined max capacity = maxBlocksCount * blockLength.
		/// </summary>
		/// <param name="blockLength">The fixed length of the individual blocks</param>
		/// <param name="maxBlocksCount">The array cannot expand beyond that value</param>
		/// <param name="initBlocksCount">The number of blocks to allocate in the constructor</param>
		/// <param name="expansionLen">A callback invoked when more blocks are needed, 
		/// allowing a nonlinear expansion.</param>
		public ConcurrentArray(
			int blockLength,
			int maxBlocksCount,
			int initBlocksCount = 1,
			Func<ConcurrentArray<T>, int> expansionLen = null)
		{
			if (blockLength < 1) throw new ArgumentException("BaseLength");
			if (initBlocksCount < 1) throw new ArgumentException("notNullsCount");
			if (expansionLen != null) ExpansionLength = expansionLen;

			blocksCount = initBlocksCount;
			BlockLength = blockLength;
			blocks = new T[maxBlocksCount][];
			direction = 1;

			for (int i = 0; i < initBlocksCount; i++)
			{
				blocks[i] = new T[blockLength];
				capacity += blockLength;
			}
		}

		/// <summary>
		/// The virtual array incremental size.
		/// </summary>
		public readonly int BlockLength;

		/// <summary>
		/// Returns the product of all block slots (including the non allocated ones) and the BlockLength.
		/// </summary>
		public int TotalMaxCapacity => blocks.Length * BlockLength;

		/// <summary>
		/// The current set index. 
		/// </summary>
		public int AppendIndex => Volatile.Read(ref appendIndex);

		/// <summary>
		/// The sum of all non null items.
		/// </summary>
		public int ItemsCount => Volatile.Read(ref notNullsCount);

		/// <summary>
		/// The total allocated space.
		/// </summary>
		public int Capacity => Volatile.Read(ref capacity);

		/// <summary>
		/// The allowed set of concurrent operations.
		/// </summary>
		public Gear Drive => (Gear)Volatile.Read(ref direction);


		/// <summary>
		/// Will be triggered when the Drive changes. 
		/// The callbacks are invoked in a new Task, wrapped in try/catch block, 
		/// i.e. all exceptions are swallowed.
		/// </summary>
		public event Action<Gear> OnGearShift;

		/// <summary>
		/// Clears the subscribers.
		/// </summary>
		public void OnGearShiftReset()
		{
			foreach (Action<Gear> s in OnGearShift.GetInvocationList())
				OnGearShift -= s;
		}

		/// <summary>
		/// Blocks until the gear is shifted. 
		/// </summary>
		/// <param name="g">The new concurrent mode.</param>
		/// <param name="f">Guarantees the execution of f() within the lock scope, in case that other shifts are waiting.</param>
		/// <param name="timeout">In milliseconds, by default is -1, which is indefinitely.</param>
		/// <returns>The old gear.</returns>
		/// <exception cref="System.SynchronizationException">Code.SignalAwaitTimeout</exception>
		public Gear ShiftGear(Gear g, Action f = null, int timeout = -1)
		{
			// One call at a time
			lock (shiftLock)
			{
				int old = -1;

				// It's not the same
				if (Drive != g)
				{
					// There must be no other resets anywhere
					gearShift.Reset();

					old = Interlocked.Exchange(ref direction, (int)g);

					// Wait for all concurrent operations to finish
					if (Volatile.Read(ref concurrentOps) > 0)
						if (!gearShift.WaitOne(timeout))
							throw new SynchronizationException(SynchronizationException.Code.SignalAwaitTimeout);

					if (OnGearShift != null)
						Task.Run(() =>
						{
							try
							{
								OnGearShift(g);
							}
							catch { }
						});
				}
				else old = (int)g;

				// Give a chance to at least one function to be executed, in case 
				// there are competing shifts.
				if (f != null) f();

				return (Gear)old;
			}
		}

		/// <summary>
		/// Access to the individual cells.
		/// </summary>
		/// <param name="index">Must be less than AppendIndex</param>
		/// <returns>The object reference at the index</returns>
		/// <exception cref="ArgumentOutOfRangeException">index</exception>
		/// <exception cref="System.InvalidOperationException">If the Drive is wrong</exception>
		public T this[int index]
		{
			get
			{
				if (Drive == Gear.P) throw new InvalidOperationException("Wrong drive");

				if (index < 0 || index > AppendIndex)
					throw new ArgumentOutOfRangeException("index");

				int seq = -1, idx = -1;
				Index2Seq(index, ref seq, ref idx);

				return Volatile.Read(ref blocks[seq][idx]);
			}
			set
			{
				if (Drive == Gear.P) throw new InvalidOperationException("Wrong drive");

				if (index < 0 || index > AppendIndex)
					throw new ArgumentOutOfRangeException("index");

				set(index, value);
			}
		}

		/// <summary>
		/// Takes the item at index and swaps it with null as one atomic operation.
		/// </summary>
		/// <remarks>
		/// One can use Take() and Append() in multi-producer, multi-consumer scenarios, since both are safe,
		/// </remarks>
		/// <param name="index">The position in the array, must be less than AppendIndex.</param>
		/// <returns>The reference at index.</returns>
		/// <exception cref="System.InvalidOperationException">When Drive is P.</exception>
		public T Take(int index)
		{
			if (Drive == Gear.P) throw new InvalidOperationException("Wrong drive");

			return set(index, null);
		}


		/// <summary>
		/// Appends an item after the AppendIndex. 
		/// If there is not enough space locks until enough blocks are
		/// allocated and then switches back to fully concurrent mode.
		/// </summary>
		/// <param name="item">The object reference</param>
		/// <returns>The index of the item, -1 if fails.</returns>
		/// <exception cref="System.InvalidOperationException">When Drive != Gear.Straight</exception>
		/// <exception cref="System.InvariantException">On attempt to append beyond TotalMaxCapacity.</exception>
		public int Append(T item)
		{
			// Must be the first instruction, otherwise a whole Shift()
			// execution could interleave after the assert and change the direction.
			var cca = Interlocked.Increment(ref concurrentOps);
			var pos = -1;

			try
			{
				if (Drive != Gear.Straight)
					throw new InvalidOperationException("Wrong drive");

				if (cca + AppendIndex >= Capacity)
				{
					lock (commonLock)
					{
						// Could have been augmented by another thread during the wait.
						var cap = Capacity; // Volatile read once
						if (cca + AppendIndex >= cap)
						{
							var newCap = ExpansionLength(this);
							// Add as much blockLenght tiles as needed.
							while (newCap > cap)
							{
								if (blocks[blocksCount] == null) blocks[blocksCount] = new T[BlockLength];
								cap += BlockLength;
								blocksCount++;
								if (blocksCount > blocks.Length)
									throw new InvariantException("blocksCount", "Maximum number of blocks reached.");
							}

							Interlocked.Exchange(ref capacity, cap);
						}
					}
				}

				if (AppendIndex < Capacity)
				{
					pos = Interlocked.Increment(ref appendIndex);
					this[pos] = item;
				}
			}
			finally
			{
				// If it's the last exiting operation for the old direction
				if (Interlocked.Decrement(ref concurrentOps) == 0 && Drive != Gear.Straight)
					gearShift.Set();
			}

			return pos;
		}

		/// <summary>
		/// Nulls the AppendIndex cell, decrements the AppendIndex value and returns the item that was there.
		/// </summary>
		/// <param name="pos">The item position.</param>
		/// <returns>The removed item.</returns>
		/// <exception cref="System.InvalidOperationException">When Drive != Gear.Reverse</exception>
		public T RemoveLast(out int pos)
		{
			var cca = Interlocked.Increment(ref concurrentOps);

			if (Drive != Gear.Reverse)
			{
				Interlocked.Decrement(ref concurrentOps);
				throw new InvalidOperationException("Wrong drive");
			}

			pos = Interlocked.Decrement(ref appendIndex) + 1;
			var item = set(pos, null);

			if (Interlocked.Decrement(ref concurrentOps) == 0 && Drive != Gear.Reverse)
				gearShift.Set();

			return item;
		}

		/// <summary>
		/// Looks for the item and nulls the array cell.
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
		/// Iterates all cells from 0 up to AppendIndex and yields the  item if it's not null at the time of the check.
		/// </summary>
		/// <returns>A not null item.</returns>
		public IEnumerable<T> Items()
		{
			int seq = -1, idx = -1;
			T item = null;

			for (int i = 0; i <= AppendIndex; i++)
			{
				Index2Seq(i, ref seq, ref idx);
				item = Volatile.Read(ref blocks[seq][idx]);
				if (item != null)
					yield return item;
			}
		}

		/// <summary>
		/// Searches for an item by traversing all cells up to AppendIndex.
		/// </summary>
		/// <param name="item">The object ref</param>
		/// <returns>A positive value if the item is found, -1 otherwise.</returns>
		public int IndexOf(T item)
		{
			int seq = -1, idx = -1, result = -1;

			for (var i = 0; i <= AppendIndex; i++)
			{
				Index2Seq(i, ref seq, ref idx);
				if (Volatile.Read(ref blocks[seq][idx]) == item)
				{
					result = i;
					break;
				}
			}

			return result;
		}

		/// <summary>
		/// Expands or shrinks the virtual array to the number of base-length tiles fitting the requested length.
		/// If the AppendIndex is greater that the new length, it's cut to length -1.
		/// If shrinking, the number of not-null values, i.e. ItemsCount is also updated.
		/// </summary>
		/// <param name="length">The new length.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">If length is negative</exception>
		/// <exception cref="System.InvalidOperationException">When Drive != Gear.P </exception>
		public void Resize(int length, bool nullBlocksOnShrink = false)
		{
			lock (commonLock)
			{
				Interlocked.Increment(ref concurrentOps);

				try
				{
					if (Drive != Gear.P) throw new InvalidOperationException("Wrong drive");

					if (length < 0 || length > TotalMaxCapacity) throw new ArgumentOutOfRangeException("length");
					if (length == Capacity) return;

					if (length > Capacity)
					{
						while (length > capacity)
						{
							if (blocks[blocksCount] == null) blocks[blocksCount] = new T[BlockLength];
							capacity += BlockLength;
							blocksCount++;
						}
					}
					else
					{
						// Leave plus one block unless resetting to zero capacity
						while ((capacity - length > BlockLength) || (length == 0 && capacity > 0))
						{
							if (nullBlocksOnShrink) blocks[blocksCount - 1] = null;
							else for (int i = 0; i < BlockLength; i++) blocks[blocksCount - 1][i] = null;
							capacity -= BlockLength;
							blocksCount--;
						}

						if (blocksCount > 0)
							for (int i = capacity - length; i < BlockLength; i++)
								blocks[blocksCount - 1][i] = null;

						Interlocked.Exchange(ref appendIndex, length - 1);

						int notNull = 0;
						foreach (var c in Items()) notNull++;

						Interlocked.Exchange(ref notNullsCount, notNull);
					}
				}
				finally
				{
					Interlocked.Decrement(ref concurrentOps);
					gearShift.Set();
				}
			}
		}

		/// <summary>
		/// Sets the provided item ref or null to all available cells.
		/// The Drive must be either N or Straight, meaning that 
		/// there is a race with Append() and set.
		/// The method is synchronized.
		/// </summary>
		/// <param name="item">The ref to be set</param>
		/// <exception cref="System.InvalidOperationException">Drive is not N or Straight</exception>
		public void Format(T item)
		{
			lock (commonLock)
			{
				Interlocked.Increment(ref concurrentOps);

				try
				{
					var d = Drive;

					if (d != Gear.N && d != Gear.Straight)
						throw new InvalidOperationException("Wrong drive");

					for (int b = 0; b < blocks.Length; b++)
						for (int i = 0; i < BlockLength; i++)
							blocks[b][i] = item;
				}
				finally
				{
					Interlocked.Decrement(ref concurrentOps);
				}
			}
		}

		/// <summary>
		/// Moves the AppendIndex to a new location, which must be and less than Capacity.
		/// </summary>
		/// <param name="newIndex">The new index.</param>
		/// <param name="forced">If true, blindly swaps the AppendIndex with the newIndex, regardless
		/// of the Drive mode.</param>
		public void MoveAppendIndex(int newIndex, bool forced = false)
		{
			if (forced) Interlocked.Exchange(ref appendIndex, newIndex);
			else
				lock (commonLock)
				{
					Interlocked.Increment(ref concurrentOps);
					try
					{
						var d = Drive;
						if (d != Gear.P)
							throw new InvalidOperationException("Wrong drive");

						if (newIndex < 0 || newIndex >= Capacity)
							throw new ArgumentOutOfRangeException("newIndex");

						var old = Interlocked.Exchange(ref appendIndex, newIndex);
					}
					finally
					{
						Interlocked.Decrement(ref concurrentOps);
					}
				}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Index2Seq(int index, ref int seq, ref int idx)
		{
			if (index < BlockLength)
			{
				seq = 0;
				idx = index;
			}
			else
			{
				seq = index / BlockLength;
				idx = index % BlockLength;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		T set(int index, T value)
		{
			int seq = -1, idx = -1;
			Index2Seq(index, ref seq, ref idx);

			var old = Interlocked.Exchange(ref blocks[seq][idx], value);

			if (old != null)
			{
				if (value == null) Interlocked.Decrement(ref notNullsCount);
			}
			else if (value != null) Interlocked.Increment(ref notNullsCount);

			return old;
		}

		/// <summary>
		/// Will be called before allocating more space, providing the current length as an argument.
		/// The returned value is desired new total length.
		/// The default growth factor is 2.
		/// </summary>
		/// <remarks>
		/// By providing different growth factors depending on the current size,
		/// one can create different allocation size vs. capacity curves.
		/// </remarks>
		Func<ConcurrentArray<T>, int> ExpansionLength = (ca) => defaultExpansion(ca);

		static int defaultExpansion(ConcurrentArray<T> arr)
		{
			if (arr.Capacity < arr.BlockLength) return arr.BlockLength;

			int newCap = arr.Capacity * 2;
			if (newCap > arr.TotalMaxCapacity) newCap = arr.TotalMaxCapacity;

			return newCap;
		}

		// Tracks the AppendIndex movement, the indexer is not counted as an op.
		int concurrentOps = 0;
		int capacity;
		int appendIndex = -1;
		int notNullsCount = 0;
		int blocksCount;
		int direction = 0;

		object commonLock = new object();
		object shiftLock = new object();

		ManualResetEvent gearShift = new ManualResetEvent(false);
		ReaderWriterLockSlim sync = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

		T[][] blocks = null;
	}
}
