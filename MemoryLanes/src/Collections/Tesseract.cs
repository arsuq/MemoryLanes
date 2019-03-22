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
	/// A virtual contiguous array backed by a 4D cube of jagged arrays. 
	/// Unlike the linked list based concurrent structures, supports indexed RW while expanding.
	/// The capacity is int.MaxValue slots.
	/// </summary>
	/// <remarks>
	/// One must inspect the Drive property before accessing the API and assert that the 
	/// concurrency mode (Gear) allows the operation. If not, one must wait for the Clutch()
	/// to transition into a correct one.
	/// </remarks>
	/// <typeparam name="T">A class</typeparam>
	public class Tesseract<T> where T : class
	{
		/// <summary>
		/// Inits a hypercube with a SIDE number of slots. 
		/// </summary>
		/// <param name="expansion">If not set DEF_EXP is used.</param>
		public Tesseract(TesseractExpansion expansion = null) : this(SIDE, true, expansion) { }

		/// <summary>
		/// Initializes the cube. If preallocating, note that the AppendIndex is -1 and the setter will blow 
		/// unless one uses Append() or MoveAppendIndex() to AllocatedSlots for example.
		/// </summary>
		/// <param name="slots">To preallocate. If not 0, the min value is SIDE.</param>
		/// <param name="countNotNulls">Can be set only here.</param>
		/// <param name="expansion">A callback that must return the desired AllocatedSlots count.</param>
		public Tesseract(int slots, bool countNotNulls, TesseractExpansion expansion = null)
		{
			if (slots > 0 && slots < SIDE) slots = SIDE;

			CountNotNulls = countNotNulls;
			this.expansion = expansion;
			blocks = new T[SIDE / 2][][][];
			direction = 1;
			allocatedSlots = alloc(slots, 0);
		}

		/// <summary>
		/// The virtual array increment length.
		/// </summary>
		public readonly int Side = SIDE;

		/// <summary>
		/// If true Append and set will update the ItemsCount.
		/// </summary>
		public readonly bool CountNotNulls;

		/// <summary>
		/// The current set index. 
		/// </summary>
		public int AppendIndex => Volatile.Read(ref appendIndex);

		/// <summary>
		/// The not null items count, -1 if CountNotNulls is false.
		/// </summary>
		public int ItemsCount => CountNotNulls ? Volatile.Read(ref notNullsCount) : -1;

		/// <summary>
		/// The allocated slots.
		/// </summary>
		public int AllocatedSlots => Volatile.Read(ref allocatedSlots);

		/// <summary>
		/// The allowed set of concurrent operations.
		/// </summary>
		public TesseractGear Drive => (TesseractGear)Volatile.Read(ref direction);

		/// <summary>
		/// Will be triggered when the Drive changes. 
		/// The callbacks are invoked in a new Task, all exceptions are swallowed.
		/// </summary>
		public event Action<TesseractGear> OnGearShift;

		/// <summary>
		/// Clears the subscribers.
		/// </summary>
		public void OnGearShiftReset()
		{
			foreach (Action<TesseractGear> s in OnGearShift.GetInvocationList())
				OnGearShift -= s;
		}

		/// <summary>
		/// Shifts the gear and blocks until all operations in the old drive mode complete.
		/// If OnGearShift is not null it's launched in a new Task, wrapped in a try catch block,
		/// swallowing all potential exceptions.
		/// </summary>
		/// <param name="g">The new concurrent mode.</param>
		/// <param name="f">Guarantees the execution of f() within the lock scope, in case that other shifts are waiting.</param>
		/// <param name="timeout">In milliseconds, by default is -1, which is indefinitely.</param>
		/// <returns>The old gear.</returns>
		/// <exception cref="System.SynchronizationException">Code.SignalAwaitTimeout</exception>
		public TesseractGear Clutch(TesseractGear g, Action f = null, int timeout = -1)
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

				return (TesseractGear)old;
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
				if (Drive == TesseractGear.P) throw new InvalidOperationException("Wrong drive");

				// Reading values is allowed beyond the Append index
				if (index < 0 || index > AllocatedSlots) throw new ArgumentOutOfRangeException("index");

				var p = new TesseractPos(index);

				return Volatile.Read(ref blocks[p.D0][p.D1][p.D2][p.D3]);
			}
			set
			{
				if (Drive == TesseractGear.P) throw new InvalidOperationException("Wrong drive");
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
			if (Drive == TesseractGear.P) throw new InvalidOperationException("Wrong drive");
			if (index < 0 || index > AllocatedSlots) throw new ArgumentOutOfRangeException("index");

			return set(index, null);
		}

		/// <summary>
		/// Appends an item after the AppendIndex. 
		/// If there is not enough space locks until enough blocks are
		/// allocated and then switches back to fully concurrent mode.
		/// </summary>
		/// <remarks>
		/// If the expansion callback throws or out-of-memory is thrown the cube should be considered unrecoverable.
		/// In that situation the gear will be jammed in Straight position and there is no way to shift it.
		/// If there are ongoing Clutch calls they will wait until their timeouts expire or forever (the default).
		/// </remarks>
		/// <param name="item">The object reference</param>
		/// <returns>The index of the item, -1 if fails.</returns>
		/// <exception cref="System.InvalidOperationException">When Drive != Gear.Straight</exception>
		/// <exception cref="System.OutOfMemoryException"></exception>
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
			Exception ex = null;

			// Try catch-ing this costs ~0.1x slowdown, 
			// If alloc throws OutOfMemory there is no hope anyways, if expansion throws...that's dumb
			if (Drive == TesseractGear.Straight)
			{
				var aidx = Volatile.Read(ref appendIndex);
				var slots = Volatile.Read(ref allocatedSlots);
				var idx = ccadd + aidx;

				if (idx >= slots)
					lock (commonLock)
					{
						// Read after the wait
						aidx = Volatile.Read(ref appendIndex);
						slots = Volatile.Read(ref allocatedSlots);
						ccadd = Volatile.Read(ref concurrentOps);
						idx = ccadd + aidx;

						if (idx >= slots)
						{
							// The default expansion assumes usage of at least x1000 slots,
							// thus the petty increments are skipped and 32 SIDE blocks are
							// constantly added. This also avoids over-committing like  
							// the List length doubling. 
							var newCap = expansion != null ? expansion(slots) : slots + DEF_EXP;

							// No capacity checks are needed since the Tesseract can store uint.MaxValue
							// slots and one can demand only int.MaxValue.

							slots = alloc(newCap, slots);
							aidx = Volatile.Read(ref appendIndex);
						}
					}

				if (aidx < slots)
				{
					pos = Interlocked.Increment(ref appendIndex);
					set(pos, item);
				}
			}
			else ex = new InvalidOperationException("Wrong drive");

			// If it's the last exiting operation for the old direction
			if (Interlocked.Decrement(ref concurrentOps) == 0 && Drive != TesseractGear.Straight)
				gearShift.Set();

			if (ex != null) throw ex;

			return pos;
		}

		/// <summary>
		/// Nulls the AppendIndex cell, decrements the AppendIndex value and returns the item that was there.
		/// </summary>
		/// <param name="pos">The item position.</param>
		/// <returns>The removed item.</returns>
		/// <exception cref="System.InvalidOperationException">When Drive != Gear.Reverse</exception>
		public T RemoveLast(ref int pos)
		{
			Interlocked.Increment(ref concurrentOps);

			Exception ex = null;
			T r = null;

			if (Drive == TesseractGear.Reverse)
			{
				pos = Interlocked.Decrement(ref appendIndex) + 1;
				r = set(pos, null);
			}
			else ex = new InvalidOperationException("Wrong drive");

			if (Interlocked.Decrement(ref concurrentOps) == 0 && Drive != TesseractGear.Reverse)
				gearShift.Set();

			if (ex != null) throw ex;

			return r;
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
		/// <param name="assertGear">If true volatile-reads the Drive at each iteration. False by default. </param>
		/// <returns>A not null item.</returns>
		/// <exception cref="InvalidOperationException">If the Drive is P</exception>
		public IEnumerable<T> NotNullItems(bool assertGear = false)
		{
			T item = null;
			var j = AppendIndex;
			var p = new TesseractPos(j);

			if (j >= 0 && blocks[p.D0] != null && blocks[p.D0].Length > p.D1 &&
				blocks[p.D0][p.D1] != null && blocks[p.D0][p.D1].Length > p.D2 &&
				blocks[p.D0][p.D1][p.D2] != null && blocks[p.D0][p.D1][p.D2].Length > p.D3)
				for (int i = 0; i <= j; i++)
				{
					if (assertGear && Drive == TesseractGear.P) throw new InvalidOperationException("Wrong drive");

					p.Set(i);

					item = Volatile.Read(ref blocks[p.D0][p.D1][p.D2][p.D3]);
					if (item != null)
						yield return item;
				}
		}

		/// <summary>
		/// Searches for an item by traversing all cells up to AppendIndex.
		/// The reads are volatile. 
		/// </summary>
		/// <param name="item">The object ref</param>
		/// <returns>A positive value if the item is found, -1 otherwise.</returns>
		public int IndexOf(T item)
		{
			int result = -1;
			int aIdx = AppendIndex;
			var p = new TesseractPos(0);

			for (var i = 0; i <= aIdx; i++)
			{
				p.Set(i);

				if (Volatile.Read(ref blocks[p.D0][p.D1][p.D2][p.D3]) == item)
				{
					result = i;
					break;
				}
			}

			return result;
		}

		/// <summary>
		/// Expands or shrinks the virtual array to the number of SIDE tiles fitting the requested length.
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
					var als = Volatile.Read(ref allocatedSlots);

					if (Drive != TesseractGear.P) throw new InvalidOperationException("Wrong drive");
					if (length < 0) throw new ArgumentOutOfRangeException("length");
					if (length == als) return;

					var p = new TesseractPos(als);

					if (length > als) alloc(length, als);
					else
					{
						while (als > length)
						{
							p.D2--;
							if (p.D2 < 1)
							{
								blocks[p.D0][p.D1] = null;
								p.D1--;
								if (p.D1 < 1)
								{
									p.D0--;
									p.D1 = SIDE - 1;
								}
								p.D2 = SIDE - 1;
							}
							else blocks[p.D0][p.D1][p.D2] = null;

							als -= SIDE;
						}

						Volatile.Write(ref allocatedSlots, als);
						Interlocked.Exchange(ref appendIndex, length - 1);

						if (CountNotNulls)
						{
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
					if (Drive != TesseractGear.N) throw new InvalidOperationException("Wrong drive");

					var p = new TesseractPos(AllocatedSlots - 1);

					for (int d0 = 0; d0 <= p.D0; d0++)
						for (int d1 = 0; d1 <= p.D1; d1++)
							for (int d2 = 0; d2 <= p.D2; d2++)
								for (int d3 = 0; d3 <= p.D3; d3++)
									blocks[d0][d1][d2][d3] = item;
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
		/// <exception cref="InvalidOperationException">The Drive is not P</exception>
		/// <exception cref="ArgumentOutOfRangeException">Index is negative or greater than AllocatedSlots</exception>
		public void MoveAppendIndex(int newIndex, bool forced = false)
		{
			if (forced) Interlocked.Exchange(ref appendIndex, newIndex);
			else
				lock (commonLock)
				{
					Interlocked.Increment(ref concurrentOps);
					try
					{
						if (Drive != TesseractGear.P) throw new InvalidOperationException("Wrong drive");
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
			var p = new TesseractPos(index);
			var old = Interlocked.Exchange(ref blocks[p.D0][p.D1][p.D2][p.D3], value);

			if (CountNotNulls)
				if (old != null)
				{
					if (value == null) Interlocked.Decrement(ref notNullsCount);
				}
				else if (value != null) Interlocked.Increment(ref notNullsCount);

			return old;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		int alloc(in int slots, int allocated)
		{
			var p = new TesseractPos(allocated);

			while (slots > allocated)
			{
				if (blocks[p.D0] == null) blocks[p.D0] = new T[SIDE][][];
				if (blocks[p.D0][p.D1] == null) blocks[p.D0][p.D1] = new T[SIDE][];
				if (blocks[p.D0][p.D1][p.D2] == null)
				{
					blocks[p.D0][p.D1][p.D2] = new T[SIDE];
					allocated += SIDE;
					p.Set(allocated);
				}
				else break;
			}

			Volatile.Write(ref allocatedSlots, allocated);

			return allocatedSlots;
		}

		// Tracks the AppendIndex movement, the indexer is not counted as an op.
		int concurrentOps;
		int allocatedSlots;
		int appendIndex = -1;
		int notNullsCount;
		int direction;

		/// <summary>
		/// A block length = 256.
		/// </summary>
		public const int SIDE = 1 << 8;
		/// <summary>
		/// The default cube expansion = 2^13 slots.
		/// </summary>
		public const int DEF_EXP = 1 << 13;

		TesseractExpansion expansion;
		object commonLock = new object();
		object shiftLock = new object();
		ManualResetEvent gearShift = new ManualResetEvent(false);

		T[][][][] blocks = null;
	}
}
