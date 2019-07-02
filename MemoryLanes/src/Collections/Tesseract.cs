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
			this.Expansion = expansion;
			blocks = new T[SIDE / 2][][][];
			i[DRIVE] = 1;
			i[SLOTS] = alloc(slots, 0);
			i[INDEX] = -1;
		}

		/// <summary>
		/// The virtual array increment length.
		/// </summary>
		public readonly int Side = SIDE;

		/// <summary>
		/// The number of slots to be added if no Expansion function is provided.
		/// </summary>
		public readonly int DefaultExpansion = DEF_EXP;

		/// <summary>
		/// If true Append and set will update the ItemsCount.
		/// </summary>
		public readonly bool CountNotNulls;

		/// <summary>
		/// The growth function.
		/// </summary>
		public readonly TesseractExpansion Expansion;

		/// <summary>
		/// The current set index. 
		/// </summary>
		public int AppendIndex => Volatile.Read(ref i[INDEX]);

		/// <summary>
		/// The not null items count, -1 if CountNotNulls is false.
		/// </summary>
		public int ItemsCount => CountNotNulls ? Volatile.Read(ref i[NNCNT]) : -1;

		/// <summary>
		/// The allocated slots.
		/// </summary>
		public int AllocatedSlots => Volatile.Read(ref i[SLOTS]);

		/// <summary>
		/// The allowed set of concurrent operations.
		/// </summary>
		public TesseractGear Drive => (TesseractGear)Volatile.Read(ref i[DRIVE]);

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

					old = Interlocked.Exchange(ref i[DRIVE], (int)g);

					// Wait for all concurrent operations to finish
					if (Volatile.Read(ref i[CCOPS]) > 0)
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
		/// <param name="index">Must be less than AllocatedSlots</param>
		/// <returns>The object reference at the index</returns>
		/// <exception cref="ArgumentOutOfRangeException">index</exception>
		/// <exception cref="System.InvalidOperationException">If the Drive is wrong</exception>
		public T this[int index]
		{
			get
			{
				if (Drive == TesseractGear.P) throw new InvalidOperationException("Wrong drive");
				if (index < 0 || index >= AllocatedSlots) throw new ArgumentOutOfRangeException("index");

				var p = new TesseractPos(index);

				return Volatile.Read(ref blocks[p.D0][p.D1][p.D2][p.D3]);
			}
			set
			{
				if (Drive == TesseractGear.P) throw new InvalidOperationException("Wrong drive");
				if (index < 0 || index >= AllocatedSlots) throw new ArgumentOutOfRangeException("index");

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
			if (index < 0 || index >= AllocatedSlots) throw new ArgumentOutOfRangeException("index");

			return set(index, null);
		}

		/// <summary>
		/// Sets value at index if the current value equals the comparand.
		/// </summary>
		/// <param name="index">The index must be less than AllocatedSlots.</param>
		/// <param name="value">The value to be set at index.</param>
		/// <param name="comparand">Will be compared to this[index]</param>
		/// <returns>The original value at index.</returns>
		public T CAS(int index, T value, T comparand)
		{
			if (Drive == TesseractGear.P) throw new InvalidOperationException("Wrong drive");
			if (index < 0 || index >= AllocatedSlots) throw new ArgumentOutOfRangeException("index");

			var p = new TesseractPos(index);
			var r = Interlocked.CompareExchange(ref blocks[p.D0][p.D1][p.D2][p.D3], value, comparand);

			if (CountNotNulls)
				if (r != null)
				{
					if (value == null) Interlocked.Decrement(ref i[NNCNT]);
				}
				else if (value != null) Interlocked.Increment(ref i[NNCNT]);

			return r;
		}

		/// <summary>
		/// Appends item after the AppendIndex. 
		/// If there is no free space left locks until enough blocks are
		/// allocated and then switches back to fully concurrent mode.
		/// </summary>
		/// <remarks>
		/// If the Expansion callback throws or out-of-memory is thrown the cube should be considered unrecoverable.
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
			var ccadd = Interlocked.Increment(ref i[CCOPS]);
			var pos = -1;
			Exception ex = null;

			// Try catch-ing this costs ~0.1x slowdown, 
			// If alloc throws OutOfMemory there is no hope anyways, if Expansion throws...that's dumb
			if (Drive == TesseractGear.Straight)
			{
				var aidx = Volatile.Read(ref i[INDEX]);
				var slots = Volatile.Read(ref i[SLOTS]);
				var idx = ccadd + aidx;

				if (idx >= slots)
					lock (commonLock)
					{
						// Read after the wait
						aidx = Volatile.Read(ref i[INDEX]);
						slots = Volatile.Read(ref i[SLOTS]);
						ccadd = Volatile.Read(ref i[CCOPS]);
						idx = ccadd + aidx;

						if (idx >= slots)
						{
							// The default Expansion assumes usage of at least x1000 slots,
							// thus the petty increments are skipped and 32 SIDE blocks are
							// constantly added. This also avoids over-committing like  
							// the List length doubling. 
							var newCap = Expansion != null ? Expansion(slots) : slots + DEF_EXP;

							if (slots < 0) slots = int.MaxValue;

							slots = alloc(newCap, slots);
							aidx = Volatile.Read(ref i[INDEX]);
						}
					}

				if (aidx < slots)
				{
					pos = Interlocked.Increment(ref i[INDEX]);
					set(pos, item);
				}
			}
			else ex = new InvalidOperationException("Wrong drive");

			// If it's the last exiting operation for the old direction
			if (Interlocked.Decrement(ref i[CCOPS]) == 0 && Drive != TesseractGear.Straight)
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
			Interlocked.Increment(ref i[CCOPS]);

			Exception ex = null;
			T r = null;

			if (Drive == TesseractGear.Reverse)
			{
				pos = Interlocked.Decrement(ref i[INDEX]) + 1;
				if (pos >= 0) r = set(pos, null);
			}
			else ex = new InvalidOperationException("Wrong drive");

			if (Interlocked.Decrement(ref i[CCOPS]) == 0 && Drive != TesseractGear.Reverse)
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
		public IEnumerable<T> NotNullItems(bool assertGear = false, bool allSlots = false)
		{
			T item = null;
			var j = allSlots ? AllocatedSlots - 1 : AppendIndex;
			var p = new TesseractPos(j);

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
		/// The reads are volatile, the comparison Object.Equals().
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

				var r = Volatile.Read(ref blocks[p.D0][p.D1][p.D2][p.D3]);

				if (r != null && r.Equals(item))
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
		/// If shrinking and counting, the number of not-null values (ItemsCount) is also updated.
		/// The Drive must be P when shrinking. 
		/// </summary>
		/// <param name="length">The new length.</param>
		/// <param name="expand">The intent of the caller.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">If length is negative</exception>
		/// <exception cref="System.InvalidOperationException">
		/// If the Drive is not P when shrinking.</exception>
		public bool Resize(int length, bool expand)
		{
			Interlocked.Increment(ref i[CCOPS]);
			TesseractGear inDrive = Drive;

			try
			{
				var slots = Volatile.Read(ref i[SLOTS]);
				if (length < 0) throw new ArgumentOutOfRangeException("length");
				if (length == slots) return false;

				lock (commonLock)
				{
					var als = Volatile.Read(ref i[SLOTS]);

					if (length == als) return false;
					if (length > als) return expand ? alloc(length, als) > als : false;
					else
					{
						if (expand) return false;

						if (inDrive != TesseractGear.P) throw new InvalidOperationException("Wrong drive");

						var toSize = length > 0 ? length + SIDE : 0;
						var p = new TesseractPos(als);

						while (als > toSize)
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

						Volatile.Write(ref i[SLOTS], als);

						if (AppendIndex >= length) Interlocked.Exchange(ref i[INDEX], length - 1);
						if (CountNotNulls)
						{
							int notNull = 0;
							foreach (var c in NotNullItems()) notNull++;
							Interlocked.Exchange(ref i[NNCNT], notNull);
						}

						return true;
					}
				}
			}
			finally
			{
				if (Interlocked.Decrement(ref i[CCOPS]) == 0 && inDrive != Drive)
					gearShift.Set();
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
				Interlocked.Increment(ref i[CCOPS]);

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
					Interlocked.Decrement(ref i[CCOPS]);
				}
			}
		}

		/// <summary>
		/// Moves the AppendIndex to a new position. If not forced, the drive must be N or P.
		/// </summary>
		/// <param name="newIndex">The new index.</param>
		/// <param name="forced">If true, blindly swaps the AppendIndex with newIndex,
		/// regardless the Drive mode or the AllocatedSlots count.</param>
		/// <exception cref="InvalidOperationException">The Drive is not N or P</exception>
		/// <exception cref="ArgumentOutOfRangeException">Index is negative or greater than AllocatedSlots</exception>
		public void MoveAppendIndex(int newIndex, bool forced = false)
		{
			if (forced) Interlocked.Exchange(ref i[INDEX], newIndex);
			else
			{
				var inDrive = Drive;
				if (inDrive == TesseractGear.Straight || inDrive == TesseractGear.Reverse) throw new InvalidOperationException("Wrong drive");
				if (newIndex < 0 || newIndex >= AllocatedSlots) throw new ArgumentOutOfRangeException("newIndex");

				Interlocked.Exchange(ref i[INDEX], newIndex);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		T set(int index, in T value)
		{
			var p = new TesseractPos(index);
			// A little more speed could be squeezed out for some ops, when 
			// the returned value is not needed and the null counting is off.
			// In these cases Volatile.Write is cheaper. 
			var old = Interlocked.Exchange(ref blocks[p.D0][p.D1][p.D2][p.D3], value);

			if (CountNotNulls)
				if (old != null)
				{
					if (value == null) Interlocked.Decrement(ref i[NNCNT]);
				}
				else if (value != null) Interlocked.Increment(ref i[NNCNT]);

			return old;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		int alloc(int slots, int allocated)
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

			Volatile.Write(ref i[SLOTS], allocated);

			return allocated;
		}

		// These are the indices for the ConcurrentOps, AllocatedSlots, AppendIndex,
		// NotNullsCount and Drive variables. The sepatation is 128 bytes.
		const int CCOPS = 32;
		const int SLOTS = 64;
		const int INDEX = 96;
		const int NNCNT = 128;
		const int DRIVE = 160;

		/// <summary>
		/// A block length = 256.
		/// </summary>
		public const int SIDE = 1 << 8;
		/// <summary>
		/// The default cube Expansion = 2^13 slots.
		/// </summary>
		public const int DEF_EXP = 1 << 13;

		object commonLock = new object();
		object shiftLock = new object();
		ManualResetEvent gearShift = new ManualResetEvent(false);

		T[][][][] blocks = null;
		int[] i = new int[161];
	}
}
