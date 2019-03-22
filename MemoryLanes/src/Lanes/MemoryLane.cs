/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace System
{
	/// <summary>
	/// A reusable memory storage with incremental allocation and ref-counted disposing behavior.
	/// Uses MemoryFragment objects for access to the allocated blocks.
	/// </summary>
	public abstract class MemoryLane : IDisposable
	{
		/// <summary>
		/// Creates a lane with the provided capacity and disposal mode.
		/// </summary>
		/// <param name="capacity">The number of bytes.</param>
		/// <param name="rm">FragmentDispose</param>
		public MemoryLane(int capacity, MemoryLaneResetMode rm)
		{
			if (capacity < HighwaySettings.MIN_LANE_CAPACITY || capacity > HighwaySettings.MAX_LANE_CAPACITY)
				throw new MemoryLaneException(MemoryLaneException.Code.SizeOutOfRange);

			ResetMode = rm;

			if (rm == MemoryLaneResetMode.TrackGhosts)
				Tracker = new Tesseract<WeakReference<MemoryFragment>>();
		}

		/// <summary>
		/// Creates a new memory fragment if there is enough space on the lane.
		/// </summary>
		/// <remarks>
		/// Calling Alloc directly on the lane competes with other potential highway calls.
		/// </remarks>
		/// <param name="size">The requested length.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <returns>Null if fails.</returns>
		public abstract MemoryFragment Alloc(int size, int tries);

		/// <summary>
		/// Attempts to allocate a fragment range in the remaining lane space.
		/// </summary>
		/// <param name="size">The requested length.</param>
		/// <param name="frag">A data bag.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <returns>True if the space was successfully taken.</returns>
		internal bool Alloc(int size, ref FragmentRange frag, int tries)
		{
			var CAP = LaneCapacity;
			var oldoffset = Volatile.Read(ref i[OFFSET]);
			var newoffset = oldoffset + size;

			while (!IsClosed && newoffset <= CAP && tries-- > 0)
			{
				// The observed offset is unchanged
				if (Interlocked.CompareExchange(ref i[OFFSET], newoffset, oldoffset) == oldoffset)
				{
					// Just makes the slot, the derived lane will set it at 'Allocation
					if (ResetMode == MemoryLaneResetMode.TrackGhosts) Tracker.Append(null);

					Volatile.Write(ref lastAllocTick, DateTime.Now.Ticks);

					frag.Allocation = Interlocked.Increment(ref i[ALLOCS]) - 1;
					frag.Length = size;
					frag.Offset = oldoffset;

					return true;
				}

				// Another thread allocated
				oldoffset = Volatile.Read(ref i[OFFSET]);
				newoffset = oldoffset + size;
			}

			return false;
		}

		/// <summary>
		/// Triggers deallocation of the tracked not-disposed and GC collected fragments. 
		/// Does nothing if the fragments are still alive. 
		/// </summary>
		/// <remarks>
		/// This method is racing both the allocations and the reset. If the lane is reset 
		/// FreeGhosts() returns immediately. The check is made on every iteration before the resetOne() call.
		/// </remarks>
		/// <returns>The number of deallocations.</returns>
		/// <exception cref="System.MemoryLaneException">Code: IncorrectDisposalMode</exception>
		public int FreeGhosts()
		{
			if (ResetMode != MemoryLaneResetMode.TrackGhosts)
				throw new MemoryLaneException(MemoryLaneException.Code.IncorrectDisposalMode);

			int freed = 0;

			if (Interlocked.CompareExchange(ref freeGhostsGate, 1, 0) < 1)
				try
				{
					int ai = Tracker.AppendIndex;
					int startedCycle = LaneCycle;

					for (int i = 0; i <= ai; i++)
					{
						var t = Tracker[i];

						if (t == null) continue;
						if (!t.TryGetTarget(out MemoryFragment f) || f == null)
						{
							// Could be a new cycle already 
							if (startedCycle != LaneCycle) return freed;
							Tracker[i] = null;
							resetOne(startedCycle);
							freed++;
						}
					}
				}
				finally
				{
					Interlocked.Exchange(ref freeGhostsGate, 0);
				}

			return freed;
		}

		/// <summary>
		/// Free is called from the fragment's Dispose method.
		/// </summary>
		/// <param name="cycle">The lane cycle given to the fragment at creation time.</param>
		protected void free(int cycle, int allocation)
		{
			if (ResetMode == MemoryLaneResetMode.TrackGhosts && cycle == LaneCycle)
				Tracker[allocation] = null;

			resetOne(cycle);
		}

		/// <summary>
		/// Tracks the provided fragment for cleanup if its Dispose method is never called 
		/// and the object is GC-ed.
		/// </summary>
		/// <param name="f">The fragment to be tracked.</param>
		/// <param name="allocationIdx">The allocation index.</param>
		protected void track(MemoryFragment f, int allocationIdx)
		{
			if (ResetMode == MemoryLaneResetMode.TrackGhosts)
			{
				if (f.LaneCycle == LaneCycle)
					Tracker[allocationIdx] = new WeakReference<MemoryFragment>(f);
				else throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessWrongLaneCycle);
			}
			else throw new MemoryLaneException(MemoryLaneException.Code.IncorrectDisposalMode);
		}

		bool resetOne(int cycle)
		{
			bool result = false;

			// Enter only if it's the correct lane cycle and there are allocations
			if (LaneCycle == cycle && Allocations > 0)
			{
				var allocs = Interlocked.Decrement(ref i[ALLOCS]);

				// If all fragments are disposed, reset the offset to 0 and change the laneCycle.
				if (allocs == 0)
				{
					Volatile.Write(ref i[OFFSET], 0);
					Interlocked.Increment(ref i[LCYCLE]);

					// The AppendIndex shift is just an Interlocked.Exchange when forced
					if (ResetMode == MemoryLaneResetMode.TrackGhosts)
						Tracker.MoveAppendIndex(0, true);

					result = true;
				}
				else if (allocs > 0) result = true;
				else throw new MemoryLaneException(MemoryLaneException.Code.LaneNegativeReset);
			}

			return result;
		}

		/// <summary>
		/// Resets the Offset and Allocations, closes the lane (optional) and increments the LaneCycle.
		/// </summary>
		/// <remarks>
		/// Resetting the offset may lead to unpredictable behavior if you attempt to read or write
		/// with any active fragments. The MemoryFragment's Read/Write and Span() methods assert the 
		/// correctness of the lane cycle, but one may already have a Span for the previous cycle,
		/// and using it will override the lane on write and yield corrupt data on read.
		/// </remarks>
		/// <param name="close">True to close the lane.</param>
		/// <param name="reset">True to reset the offset and the allocations to 0.</param>
		public void Force(bool close, bool reset = false)
		{
			Interlocked.Exchange(ref isClosed, close ? 1 : 0);

			if (reset)
			{
				Interlocked.Exchange(ref i[OFFSET], 0);
				Interlocked.Exchange(ref i[ALLOCS], 0);
				Interlocked.Increment(ref i[LCYCLE]);

				if (ResetMode == MemoryLaneResetMode.TrackGhosts)
					Tracker.MoveAppendIndex(0, true);
			}
		}

		/// <summary>
		/// Writes count bytes from source into the lane space. The lane is forced to Offset 0 and is closed.
		/// If fails the lane remains closed.
		/// </summary>
		/// <remarks>This method is synchronized, but not protected from concurrent Force() calls.
		/// </remarks>
		/// <param name="source">The source stream.</param>
		/// <param name="count">Number of bytes to copy from source.</param>
		/// <returns>The copied bytes count.</returns>
		/// <exception cref="ArgumentOutOfRangeException">The count is less than 1 or greater than LaneCapacity</exception>
		public int Format(Stream source, int count)
		{
			if (count < 1 || count > LaneCapacity) throw new ArgumentOutOfRangeException("count");

			var read = 0;

			lock (formatGate)
			{
				Force(false, true);

				using (var f = Alloc(LaneCapacity, 1))
				{
					Interlocked.Exchange(ref isClosed, 1);

					if (f == null) throw new MemoryLaneException(MemoryLaneException.Code.AllocFailure);

					f.UseAccessChecks = false;

					using (var fs = f.CreateStream())
						read = ((Stream)fs).ReadFrom(source, count).Result;
				}

				Interlocked.Exchange(ref isClosed, 0);
			}

			return read;
		}

		/// <summary>
		/// Traces the Allocations, LaneCycle, Capacity, Offset, LasrtAllocTick and IsClosed properties.
		/// </summary>
		/// <returns>A formatted string: [offset/cap #allocations C:LaneCycle T:lastAllocTick on/off]</returns>
		public virtual string FullTrace() =>
			$"[{Offset}/{LaneCapacity} #{Allocations} C:{LaneCycle} T:{DateTime.Now.Ticks - LastAllocTick} {(IsClosed ? "off" : "on")}]";

		/// <summary>
		/// The lane capacity in bytes.
		/// </summary>
		public abstract int LaneCapacity { get; }
		public abstract void Dispose();

		/// <summary>
		/// The lane offset index.
		/// </summary>
		public int Offset => Volatile.Read(ref i[OFFSET]);

		/// <summary>
		/// The number of allocations so far. Resets to zero on a new cycle.
		/// </summary>
		public int Allocations => Volatile.Read(ref i[ALLOCS]);

		/// <summary>
		/// The last allocation timer tick.
		/// </summary>
		public long LastAllocTick => Volatile.Read(ref lastAllocTick);

		/// <summary>
		/// If the lane is closed. This is flag is not related with IsDisposed.
		/// </summary>
		public bool IsClosed => Volatile.Read(ref isClosed) > 0;

		/// <summary>
		/// The current lane cycle, i.e. number of resets.
		/// </summary>
		public int LaneCycle => Volatile.Read(ref i[LCYCLE]);

		/// <summary>
		/// If true the underlying memory storage is released.
		/// </summary>
		public bool IsDisposed => Volatile.Read(ref isDisposed);

		public const int SPIN_GATE_AWAIT_MS = 10;
		public const int SPIN_GATE_BAIL_MS = 80;
		public const int TRACKER_ASSUMED_MIN_FRAG_SIZE_BYTES = 32;

		protected bool isDisposed;
		protected long lastAllocTick;

		// The indices of Allocations, Offset and LaneCycle
		protected const int ALLOCS = 32;
		protected const int OFFSET = 64;
		protected const int LCYCLE = 96;

		public readonly MemoryLaneResetMode ResetMode;

		// The Tracker index is the Allocation index of the fragment
		Tesseract<WeakReference<MemoryFragment>> Tracker = null;

		object formatGate = new object();
		object allocGate = new object();
		int freeGhostsGate;
		int isClosed;

		protected int[] i = new int[97];
	}

	/// <summary>
	/// Determines the way lanes deal with fragment deallocation.
	/// </summary>
	public enum MemoryLaneResetMode : int
	{
		/// <summary>
		/// If the consumer forgets to call dispose on a fragment,
		/// the lane will never reset unless forced.
		/// </summary>
		FragmentDispose = 0,
		/// <summary>
		/// The lane will track all fragments into a special collection
		/// of weak refs, so when the GC collects the non-disposed fragments
		/// the lane can deallocate the correct amount and reset. 
		/// The triggering of the deallocation - FreeGhosts() is consumers responsibility.
		/// </summary>
		TrackGhosts = 1
	}

	struct FragmentRange
	{
		public FragmentRange(int offset, int length, int allocation)
		{
			Offset = offset;
			Length = length;
			Allocation = allocation;
		}

		public int Offset;
		public int Length;
		public int Allocation;
	}
}