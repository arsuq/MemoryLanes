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
		public MemoryLane(int capacity)
		{
			if (capacity < HighwaySettings.MIN_LANE_CAPACITY || capacity > HighwaySettings.MAX_LANE_CAPACITY)
				throw new MemoryLaneException(MemoryLaneException.Code.SizeOutOfRange);
		}

		/// <summary>
		/// Creates a new memory fragment if there is enough space on the lane.
		/// </summary>
		/// <remarks>
		/// Calling Alloc directly on the lane competes with other potential highway calls.
		/// </remarks>
		/// <param name="size">The requested length.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <param name="awaitMS">The awaitMS for each try</param>
		/// <returns>Null if fails.</returns>
		public abstract MemoryFragment Alloc(int size, int tries, int awaitMS = 8);

		/// <summary>
		/// Attempts to allocate a fragment range in the remaining lane space.
		/// </summary>
		/// <param name="size">The requested length.</param>
		/// <param name="frag">A data bag.</param>
		/// <param name="tries">The number of fails before switching to another lane.</param>
		/// <param name="awaitMS">The awaitMS for each try</param>
		/// <returns>True if the space was successfully taken.</returns>
		internal bool Alloc(int size, ref FragmentRange frag, int tries, int awaitMS)
		{
			var CAP = LaneCapacity;
			var r = false;

			if (Volatile.Read(ref i[OFFSET]) + size <= CAP)
				while (isDisposed < 1 && !IsClosed && tries-- > 0 && !r && Monitor.TryEnter(allocGate, awaitMS))
				{
					var offset = i[OFFSET];

					if (offset + size <= CAP)
					{
						i[OFFSET] += size;

						// The reset locks only when 0.
						frag.Allocation = Interlocked.Increment(ref i[ALLOCS]);
						frag.Length = size;
						frag.Offset = offset;
						frag.LaneCycle = i[LCYCLE];

						lastAllocTick = DateTime.Now.Ticks;

						r = true;
					}

					Monitor.Exit(allocGate);
				}

			return r;
		}

		protected bool resetOne(int cycle)
		{
			// [i] The Interlocked.Decrement forces an Increment in Alloc.
			// This should prevent queues of disposed frags, but slows down Alloc.

			if (LaneCycle != cycle) return false;
			if (Interlocked.Decrement(ref i[ALLOCS]) == 0)
				lock (allocGate)
				{
					// Check again if all fragments are disposed.
					if (i[ALLOCS] == 0)
					{
						i[OFFSET] = 0;
						i[LCYCLE]++;
					}
					else if (i[ALLOCS] < 0)
						throw new MemoryLaneException(MemoryLaneException.Code.LaneNegativeReset);
				}

			return true;
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
				lock (allocGate)
				{
					i[LCYCLE] += 1;
					i[OFFSET] = 0;
					i[ALLOCS] = 0;
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

				using (var f = Alloc(LaneCapacity, 1, 1000))
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
		/// For diagnostics.
		/// </summary>
		public abstract ReadOnlySpan<byte> GetAllBytes();

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
		public bool IsDisposed => Volatile.Read(ref isDisposed) > 0;

		protected int isDisposed;
		protected long lastAllocTick;

		// The indices of Allocations, Offset and LaneCycle
		protected const int ALLOCS = 32;
		protected const int OFFSET = 64;
		protected const int LCYCLE = 96;

		object formatGate = new object();
		object allocGate = new object();
		int isClosed;

		protected int[] i = new int[97];
	}

	struct FragmentRange
	{
		public FragmentRange(int offset, int length, int allocation, int cycle)
		{
			Offset = offset;
			Length = length;
			Allocation = allocation;
			LaneCycle = cycle;
		}

		public int Offset;
		public int Length;
		public int Allocation;
		public int LaneCycle;
	}
}