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
	/// One must inspect the Drive property before accessing the API and assert that the 
	/// concurrency mode (Gear) allows the operation. If not, one must wait for the ShiftGear()
	/// to transition into a correct one.
	/// </remarks>
	/// <typeparam name="T">A class</typeparam>
	public class CCube<T> where T : class
	{
		/// <summary>
		/// Allocates an array of arrays with side = sqrt(capacity) 
		/// </summary>
		/// <param name="expansion">A callback that must return the desired AllocatedSlots count 
		/// calculated from the args: AllocatedSlots, BlockLength and Capacity.</param>
		/// <exception cref="ArgumentOutOfRangeException">If the capacity is negative or zero.</exception>
		public CCube(CCArrayExpansion expansion = null)
		{
			this.expansion = expansion != null ? expansion : defaultExpansion;

			BlockLength = SIDE;
			blocks = new T[SIDE][][];
			direction = 1;

			// Allocate one block
			blocks[0] = new T[SIDE][];
			blocks[0][0] = new T[SIDE];

			allocatedSlots += SIDE;
		}

		/// <summary>
		/// The virtual array incremental size.
		/// </summary>
		public readonly int BlockLength;

		/// <summary>
		/// The maximum number of slots that could be allocated.
		/// </summary>
		public int Capacity => blocks.Length * BlockLength;

		/// <summary>
		/// The current set index. 
		/// </summary>
		public int AppendIndex => Volatile.Read(ref appendIndex);

		/// <summary>
		/// The sum of all non null items.
		/// </summary>
		public int ItemsCount => Volatile.Read(ref notNullsCount);

		/// <summary>
		/// The allocated slots.
		/// </summary>
		public int AllocatedSlots => Volatile.Read(ref allocatedSlots);

		/// <summary>
		/// The allowed set of concurrent operations.
		/// </summary>
		public CCArrayGear Drive => (CCArrayGear)Volatile.Read(ref direction);

		/// <summary>
		/// Will be triggered when the Drive changes. 
		/// The callbacks are invoked in a new Task, wrapped in try/catch block, 
		/// i.e. all exceptions are swallowed.
		/// </summary>
		public event Action<CCArrayGear> OnGearShift;

		/// <summary>
		/// Clears the subscribers.
		/// </summary>
		public void OnGearShiftReset()
		{
			foreach (Action<CCArrayGear> s in OnGearShift.GetInvocationList())
				OnGearShift -= s;
		}

		/// <summary>
		/// Shifts the gear and blocks until all operations in the old drive mode complete.
		/// If OnGearShift is not null it's launched in a new Task, wrapped in a try catch, swallowing potential exceptions.
		/// </summary>
		/// <param name="g">The new concurrent mode.</param>
		/// <param name="f">Guarantees the execution of f() within the lock scope, in case that other shifts are waiting.</param>
		/// <param name="timeout">In milliseconds, by default is -1, which is indefinitely.</param>
		/// <returns>The old gear.</returns>
		/// <exception cref="System.SynchronizationException">Code.SignalAwaitTimeout</exception>
		public CCArrayGear ShiftGear(CCArrayGear g, Action f = null, int timeout = -1)
		{
			// One call at a time
			lock (shiftLock)
			{
				int old = -1;

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

				// Give a chance to at least one function to be executed,
				// in case there are competing shifts.
				if (f != null) f();

				return (CCArrayGear)old;
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
				if (Drive == CCArrayGear.P) throw new InvalidOperationException("Wrong drive");
				if (index < 0 || index > AppendIndex) throw new ArgumentOutOfRangeException("index");

				var p = new CCubePos(index);

				return Volatile.Read(ref blocks[p.D0][p.D1][p.D2]);
			}
			set
			{
				if (Drive == CCArrayGear.P) throw new InvalidOperationException("Wrong drive");
				if (index < 0 || index > AppendIndex) throw new ArgumentOutOfRangeException("index");

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
			if (Drive == CCArrayGear.P) throw new InvalidOperationException("Wrong drive");

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
		/// <exception cref="System.InvariantException">On attempt to append beyond Capacity.</exception>
		public int Append(T item)
		{
			// Must be the first instruction, otherwise a whole Shift()
			// execution could interleave after the assert and change the direction.
			Interlocked.Increment(ref concurrentOps);
			var ccadd = Interlocked.Increment(ref concurrentAdds);
			var pos = -1;

			try
			{
				if (Drive != CCArrayGear.Straight) throw new InvalidOperationException("Wrong drive");
				if (ccadd + AppendIndex >= AllocatedSlots)
					lock (commonLock)
					{
						// Volatile read once
						var cap = AllocatedSlots;
						// Could have been augmented by another thread during the wait.
						if (ccadd + AppendIndex >= cap)
						{
							var p = new CCubePos(AllocatedSlots);
							var d1b = p.D1;
							var newCap = expansion(AllocatedSlots, BlockLength, Capacity);
							while (newCap > cap)
							{
								if (blocks[d0b] == null) blocks[d0b] = new T[SIDE][];
								if (blocks[d0b][d1b] == null)
								{
									blocks[d0b][d1b] = new T[SIDE];
									cap += SIDE;
								}

								d1b++;

								if (d1b > SIDE)
								{
									d0b++;
									d1b = 0;
								}
							}

							Interlocked.Exchange(ref allocatedSlots, cap);
						}
					}

				if (AppendIndex < AllocatedSlots)
				{
					pos = Interlocked.Increment(ref appendIndex);
					this[pos] = item;
				}
			}
			finally
			{
				Interlocked.Decrement(ref concurrentAdds);

				// If it's the last exiting operation for the old direction
				if (Interlocked.Decrement(ref concurrentOps) == 0 && Drive != CCArrayGear.Straight)
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
			Interlocked.Increment(ref concurrentOps);

			if (Drive != CCArrayGear.Reverse)
			{
				Interlocked.Decrement(ref concurrentOps);
				throw new InvalidOperationException("Wrong drive");
			}

			pos = Interlocked.Decrement(ref appendIndex) + 1;
			var item = set(pos, null);

			if (Interlocked.Decrement(ref concurrentOps) == 0 && Drive != CCArrayGear.Reverse)
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
		/// Iterates all cells from 0 up to AppendIndex and yields each item
		/// if it's not null at the time of the check.
		/// </summary>
		/// <remarks>
		/// Doesn't check for potential resizing in the loop (Gear.P), because the traversal could
		/// be very slow (can't lock) or very fast (the assert is too expensive on the cache).
		/// The Consumer could guarantee the correct drive before calling this method.
		/// </remarks>
		/// <returns>A not null item.</returns>
		public IEnumerable<T> NotNullItems()
		{
			T item = null;
			var p = new CCubePos(AppendIndex);

			if (blocks[p.D0] != null && blocks[p.D0].Length > p.D1 &&
				blocks[p.D0][p.D1] != null && blocks[p.D0][p.D1].Length > p.D2)
				for (int i = 0; i <= AppendIndex; i++)
				{
					p = new CCubePos(i);

					item = Volatile.Read(ref blocks[p.D0][p.D1][p.D2]);
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
			int result = -1;

			for (var i = 0; i <= AppendIndex; i++)
			{
				var p = new CCubePos(i);
				if (Volatile.Read(ref blocks[p.D0][p.D1][p.D2]) == item)
				{
					result = i;
					break;
				}
			}

			return result;
		}

		/// <summary>
		/// Expands or shrinks the virtual array (within the range of capacity) to the number of base-length tiles 
		/// fitting the requested length.
		/// If the AppendIndex is greater that the new length, it's cut to length -1.
		/// If shrinking, the number of not-null values, i.e. ItemsCount is also updated.
		/// </summary>
		/// <param name="length">The new length.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">If length is negative or greater than the capacity.</exception>
		/// <exception cref="System.InvalidOperationException">When Drive != Gear.P </exception>
		public void Resize(int length)
		{
			lock (commonLock)
			{
				Interlocked.Increment(ref concurrentOps);

				try
				{
					if (Drive != CCArrayGear.P) throw new InvalidOperationException("Wrong drive");
					if (length < 0 || length > Capacity) throw new ArgumentOutOfRangeException("length");
					if (length == AllocatedSlots) return;

					var p = new CCubePos(AllocatedSlots);
					var d1b = p.D1;

					if (length > AllocatedSlots)
					{
						while (length > allocatedSlots)
						{
							if (blocks[d0b] == null)
							{
								blocks[d0b] = new T[BlockLength][];
								d0b++;
								d1b = 0;
							}

							if (blocks[d0b][d1b] == null)
							{
								blocks[d0b][d1b] = new T[BlockLength];
								allocatedSlots += BlockLength;
							}

							d1b++;
						}
					}
					else
					{
						while (allocatedSlots > length)
						{
							d1b--;
							if (d1b < 1)
							{
								blocks[d0b] = null;
								d0b--;
								d1b = BlockLength;
							}
							else blocks[d0b][d1b] = null;

							allocatedSlots -= BlockLength;
						}

						Interlocked.Exchange(ref appendIndex, length - 1);

						if (d0b < 0) d0b = 0;
						int notNull = 0;
						foreach (var c in NotNullItems()) notNull++;

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
		/// Sets the provided item (ref or null) to all available cells.
		/// The Drive must be N.
		/// </summary>
		/// <remarks>The method is synchronized.</remarks>
		/// <param name="item">The ref to be set</param>
		/// <exception cref="System.InvalidOperationException">Drive is not N or Straight</exception>
		public void Format(T item)
		{
			lock (commonLock)
			{
				Interlocked.Increment(ref concurrentOps);

				try
				{
					if (Drive != CCArrayGear.N) throw new InvalidOperationException("Wrong drive");

					var p = new CCubePos(AllocatedSlots - 1);

					for (int d0 = 0; d0 <= p.D0; d0++)
						for (int d1 = 0; d1 <= p.D1; d1++)
							for (int d2 = 0; d2 <= p.D2; d2++)
								blocks[d0][d1][d2] = item;
				}
				finally
				{
					Interlocked.Decrement(ref concurrentOps);
				}
			}
		}

		/// <summary>
		/// Moves the AppendIndex to a new, than Capacity position.
		/// The drive must be P.
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
						if (Drive != CCArrayGear.P) throw new InvalidOperationException("Wrong drive");
						if (newIndex < 0 || newIndex >= AllocatedSlots) throw new ArgumentOutOfRangeException("newIndex");

						Interlocked.Exchange(ref appendIndex, newIndex);
					}
					finally
					{
						Interlocked.Decrement(ref concurrentOps);
					}
				}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		T set(int index, T value)
		{
			var p = new CCubePos(index);
			var old = Interlocked.Exchange(ref blocks[p.D0][p.D1][p.D2], value);

			if (old != null)
			{
				if (value == null) Interlocked.Decrement(ref notNullsCount);
			}
			else if (value != null) Interlocked.Increment(ref notNullsCount);

			return old;
		}

		int defaultExpansion(int allocatedSlots, int blockLength, int capacity)
		{
			if (allocatedSlots < blockLength) return blockLength;

			int newCap = allocatedSlots * 2;
			if (newCap > capacity) newCap = capacity;

			return newCap;
		}

		// Tracks the AppendIndex movement, the indexer is not counted as an op.
		int concurrentOps;
		int concurrentAdds;
		int allocatedSlots;
		int appendIndex = -1;
		int notNullsCount;
		int d0b;
		int direction;

		// A magic number such that SIDE^3 fits int.MaxValue 
		const int SIDE = 1291;
		const int PLANE = 1291 * 1291;

		CCArrayExpansion expansion;
		object commonLock = new object();
		object shiftLock = new object();
		ManualResetEvent gearShift = new ManualResetEvent(false);

		T[][][] blocks = null;
	}
}
