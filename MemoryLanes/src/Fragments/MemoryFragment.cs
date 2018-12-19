using System.Runtime.InteropServices;

namespace System
{
	public abstract class MemoryFragment : IDisposable
	{
		public MemoryFragment() { }

		public MemoryFragment(MemoryLane lane)
		{
			this.lane = lane ?? throw new ArgumentNullException("lane");
			laneCycle = lane.LaneCycle;
		}

		public abstract int Write(byte[] data, int offset, int length);
		public abstract int Read(byte[] destination, int offset, int destOffset = 0);
		public abstract Span<byte> Span();
		public abstract int Length { get; }
		public abstract void Dispose();

		public bool IsDisposed => isDisposed;
		public long LaneCycle => laneCycle;

		protected void laneCheck()
		{
			if (!useAccessChecks) return;
			if (LaneCycle != Lane.LaneCycle) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessWrongLaneCycle);
			if (Lane.IsClosed) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessClosedLane);
			if (Lane.IsDisposed) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessDisposedLane);
		}

		/// <summary>
		/// If true checks whether the lane is closed, disposed or the cycle is not changed.
		/// Derived classes may expose it as public, by default is true.
		/// </summary>
		protected bool useAccessChecks = true;

		public Span<T> ToSpan<T>() where T : struct =>
			MemoryMarshal.Cast<byte, T>(Span());

		public MemoryLane Lane => lane;
		protected MemoryLane lane;
		protected readonly int laneCycle;
		protected bool isDisposed;
	}
}
