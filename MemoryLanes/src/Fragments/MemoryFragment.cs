/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
	public abstract class MemoryFragment : IDisposable
	{
		public MemoryFragment() { }

		public MemoryFragment(MemoryLane lane, Action dtor)
		{
			this.lane = lane ?? throw new ArgumentNullException("lane");
			Destructor = dtor ?? throw new ArgumentNullException("dtor");
			laneCycle = lane.LaneCycle;
		}

		public abstract int Write(byte[] data, int offset, int length);
		public abstract int Read(byte[] destination, int offset, int destOffset = 0);
		public abstract Span<byte> Span();
		public abstract int Length { get; }
		public abstract void Dispose();

		public bool IsDisposed => isDisposed;
		public int LaneCycle => laneCycle;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void laneCheck()
		{
			if (!useAccessChecks) return;
			if (LaneCycle != Lane.LaneCycle) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessWrongLaneCycle);
			if (Lane.IsClosed) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessClosedLane);
			if (Lane.IsDisposed) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessDisposedLane);
		}

		/// <summary>
		/// If true checks whether the lane is closed, disposed or the cycle has not changed.
		/// Derived classes may expose it as public, by default is true.
		/// </summary>
		protected bool useAccessChecks = true;

		public Span<T> ToSpan<T>() where T : struct =>
			MemoryMarshal.Cast<byte, T>(Span());

		public MemoryLane Lane => lane;
		protected MemoryLane lane;
		protected readonly int laneCycle;
		protected bool isDisposed;
		protected readonly Action Destructor;
	}
}
