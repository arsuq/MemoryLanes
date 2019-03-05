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
	/// An expansion calculator for the CCube.
	/// An instance is called whenever more blocks are needed.
	/// One could expand differently, depending on the current array size.
	/// </summary>
	/// <param name="allocatedSlots">The current AllocationSlots value.</param>
	/// <returns>The desired new AllocatedSlots count.</returns>
	public delegate int CCubeExpansion(in int allocatedSlots);

	/// <summary>
	/// The allowed concurrent operations on a CCube
	/// </summary>
	public enum CCubeGear
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
		/// Concurrent RemoveLast() gets and sets are enabled.
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

	/// <summary>
	/// A virtual contiguous array backed by a cube of jagged arrays. 
	/// Unlike the linked list based concurrent structures, supports parallel indexed RW and expansion.
	/// The capacity is (int.MaxValue + 1) / 2 slots.
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
		/// Init a cube with a SIDE number of slots.
		/// </summary>
		/// <param name="expansion">If not set DEF_EXP is used.</param>
		public CCube(CCubeExpansion expansion = null) : this(SIDE, true, expansion) { }

		/// <summary>
		/// Creates the cube with initBlocks * SIDE slots. Note that the AppendIndex is -1 and 
		/// the setter will blow unless one uses Append() or MoveAppendIndex() to AllocatedSlots for example.
		/// </summary>
		/// <param name="slots">To preallocate. The min value is SIDE.</param>
		/// <param name="countNotNulls">Can be set only here.</param>
		/// <param name="expansion">A callback that must return the desired AllocatedSlots count.</param>
		public CCube(int slots, bool countNotNulls, CCubeExpansion expansion = null)
		{
			if (slots < SIDE) slots = SIDE;
			if (slots > CAPACITY) throw new ArgumentOutOfRangeException("slots");

			CountNotNulls = countNotNulls;
			this.expansion = expansion;
			blocks = new T[SIDE][][];
			direction = 1;

			var d1b = 0;
			while (slots > allocatedSlots)
			{
				if (blocks[d0] == null) blocks[d0] = new T[SIDE][];
				if (blocks[d0][d1b] == null)
				{
					blocks[d0][d1b] = new T[SIDE];
					allocatedSlots += SIDE;
				}

				d1b++;

				if (d1b >= SIDE)
				{
					d0++;
					d1b = 0;
				}
			}
		}

		/// <summary>
		/// The virtual array incremental size.
		/// </summary>
		public readonly int BlockLength = SIDE;

		/// <summary>
		/// The maximum number of slots that could be allocated.
		/// </summary>
		public readonly int Capacity = CAPACITY;

		/// <summary>
		/// If true Append and set will update the ItemsCount.
		/// </summary>
		public readonly bool CountNotNulls;

		/// <summary>
		/// The current set index. 
		/// </summary>
		public int AppendIndex => Volatile.Read(ref appendIndex);

		/// <summary>
		/// The not null items count.
		/// </summary>
		public int ItemsCount => Volatile.Read(ref notNullsCount);

		/// <summary>
		/// The allocated slots.
		/// </summary>
		public int AllocatedSlots => Volatile.Read(ref allocatedSlots);

		/// <summary>
		/// The allowed set of concurrent operations.
		/// </summary>
		public CCubeGear Drive => (CCubeGear)Volatile.Read(ref direction);

		/// <summary>
		/// Will be triggered when the Drive changes. 
		/// The callbacks are invoked in a new Task, all exceptions are swallowed.
		/// </summary>
		public event Action<CCubeGear> OnGearShift;

		/// <summary>
		/// Clears the subscribers.
		/// </summary>
		public void OnGearShiftReset()
		{
			foreach (Action<CCubeGear> s in OnGearShift.GetInvocationList())
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
		public CCubeGear ShiftGear(CCubeGear g, Action f = null, int timeout = -1)
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

				return (CCubeGear)old;
			}
		}

		/// <summary>
		/// Access to the individual cells.
		/// </summary>
		/// <remarks>
		/// Reading is allowed beyond the AppendIndex if less than AllocatedSlots. This means that 
		/// RemoveLast() and get can safely be executed concurrently.
		/// </remarks>
		/// <param name="index">For set must be less than AppendIndex, for get less than AllocatedSlots </param>
		/// <returns>The object reference at the index</returns>
		/// <exception cref="ArgumentOutOfRangeException">index</exception>
		/// <exception cref="System.InvalidOperationException">If the Drive is wrong</exception>
		public T this[int index]
		{
			get
			{
				if (Drive == CCubeGear.P) throw new InvalidOperationException("Wrong drive");

				// Reading values is allowed beyond the Append index
				if (index < 0 || index > AllocatedSlots) throw new ArgumentOutOfRangeException("index");

				var p = new CCubePos(index);

				return Volatile.Read(ref blocks[p.D0][p.D1][p.D2]);
			}
			set
			{
				if (Drive == CCubeGear.P) throw new InvalidOperationException("Wrong drive");
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
		/// <param name="index">The position in the array, must be less than AllocatedSlots.</param>
		/// <returns>The reference at index.</returns>
		/// <exception cref="System.InvalidOperationException">When Drive is P.</exception>
		/// <exception cref="ArgumentOutOfRangeException">The index is negative or beyond AllocatedSlots.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Take(int index)
		{
			if (Drive == CCubeGear.P) throw new InvalidOperationException("Wrong drive");
			if (index < 0 || index > AllocatedSlots) throw new ArgumentOutOfRangeException("index");

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
		/// <exception cref="System.OutOfMemoryException">Guess what</exception>
		public int Append(T item)
		{
			// Must increment the ccops before the drive check, otherwise a whole Shift() execution
			// could interleave after the gear assert and change the direction.
			// Although it's possible other threads to false increment the ccops
			// all other ops will throw WrongDrive and decrement it. 
			// Using the ccops instead of a dedicated ccAppends counter saves two atomics,
			// in this already not-cache-friendly structure.
			var ccadd = Interlocked.Increment(ref concurrentOps);
			var pos = -1;

			try
			{
				// Check if the Drive is CCubeGear.Straight
				if (Volatile.Read(ref direction) != 1) throw new InvalidOperationException("Wrong drive");

				var aidx = Volatile.Read(ref appendIndex);
				var slots = Volatile.Read(ref allocatedSlots);
				var idx = ccadd + aidx;

				if (idx < CAPACITY && idx >= slots)
					lock (commonLock)
					{
						// This can be optimized to release the awaiting appends all at once after the expansion,
						// since the smallest augmentation is SIDE slots, i.e. there must be 1025 queued threads 
						// and a single SIDE expansion before having a missed append.

						// Read after the wait
						aidx = Volatile.Read(ref appendIndex);
						slots = Volatile.Read(ref allocatedSlots);
						ccadd = Volatile.Read(ref concurrentOps);
						idx = ccadd + aidx;

						if (idx < CAPACITY && idx >= slots)
						{
							var p = new CCubePos(slots);
							var d1 = p.D1;

							// The default expansion assumes at least x10K slots usage,
							// thus the petty increments from 1K to 32K are skipped and 32 SIDE blocks are
							// constantly added. This also avoids over-committing such as 
							// the Count doubling used in List for example. 
							var newCap = expansion != null ? expansion(slots) : allocatedSlots + DEF_EXP;
							if (newCap > CAPACITY) newCap = CAPACITY;

							while (newCap > slots)
							{
								if (blocks[d0] == null) blocks[d0] = new T[SIDE][];
								if (blocks[d0][d1] == null)
								{
									// These blocks are GC friendly. 
									blocks[d0][d1] = new T[SIDE];
									slots += SIDE;
								}
								if (++d1 >= SIDE) { d0++; d1 = 0; }
							}

							Volatile.Write(ref allocatedSlots, slots);
							aidx = Volatile.Read(ref appendIndex);
						}
					}

				if (aidx < slots)
				{
					pos = Interlocked.Increment(ref appendIndex);
					set(pos, item);
				}
			}
			finally
			{
				// If it's the last exiting operation for the old direction
				if (Interlocked.Decrement(ref concurrentOps) == 0 && Volatile.Read(ref direction) != 1)
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

			if (Drive != CCubeGear.Reverse)
			{
				Interlocked.Decrement(ref concurrentOps);
				throw new InvalidOperationException("Wrong drive");
			}

			pos = Interlocked.Decrement(ref appendIndex) + 1;
			var item = set(pos, null);

			if (Interlocked.Decrement(ref concurrentOps) == 0 && Drive != CCubeGear.Reverse)
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
			var j = AppendIndex;
			var p = new CCubePos(j);

			if (j >= 0 && blocks[p.D0] != null && blocks[p.D0].Length > p.D1 &&
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
					if (Drive != CCubeGear.P) throw new InvalidOperationException("Wrong drive");
					if (length < 0 || length > Capacity) throw new ArgumentOutOfRangeException("length");
					if (length == AllocatedSlots) return;

					var a = AllocatedSlots;
					var p = new CCubePos(a);
					var d1 = p.D1;

					if (length > a)
					{
						while (length > allocatedSlots)
						{
							if (blocks[d0] == null) blocks[d0] = new T[SIDE][];
							if (blocks[d0][d1] == null)
							{
								blocks[d0][d1] = new T[SIDE];
								allocatedSlots += SIDE;
							}

							d1++;

							if (d1 > SIDE) { d0++; d1 = 0; }
						}
					}
					else
					{
						while (allocatedSlots > length)
						{
							d1--;
							if (d1 < 1)
							{
								blocks[d0] = null;
								d0--;
								d1 = BlockLength;
							}
							else blocks[d0][d1] = null;

							allocatedSlots -= BlockLength;
						}

						Interlocked.Exchange(ref appendIndex, length - 1);

						if (CountNotNulls)
						{
							if (d0 < 0) d0 = 0;
							int notNull = 0;
							foreach (var c in NotNullItems()) notNull++;

							Interlocked.Exchange(ref notNullsCount, notNull);
						}
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
					if (Drive != CCubeGear.N) throw new InvalidOperationException("Wrong drive");

					var a = AllocatedSlots - 1;
					var p = new CCubePos(a);

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
						if (Drive != CCubeGear.P) throw new InvalidOperationException("Wrong drive");
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
		T set(in int index, in T value)
		{
			var p = new CCubePos(index);
			var old = Interlocked.Exchange(ref blocks[p.D0][p.D1][p.D2], value);

			if (CountNotNulls)
				if (old != null)
				{
					if (value == null) Interlocked.Decrement(ref notNullsCount);
				}
				else if (value != null) Interlocked.Increment(ref notNullsCount);

			return old;
		}

		// Tracks the AppendIndex movement, the indexer is not counted as an op.
		int concurrentOps;
		int allocatedSlots;
		int appendIndex = -1;
		int notNullsCount;
		int direction;
		// The main side index
		int d0;

		/// <summary>
		/// A block length = 1024.
		/// </summary>
		public const int SIDE = 1 << 10;
		/// <summary>
		/// The default cube expansion = 2^15 slots.
		/// </summary>
		public const int DEF_EXP = 1 << 15;
		public const int CAPACITY = 1 << 30;
		const int PLANE = 1 << 20;

		CCubeExpansion expansion;
		object commonLock = new object();
		object shiftLock = new object();
		ManualResetEvent gearShift = new ManualResetEvent(false);

		T[][][] blocks = null;
	}
}
