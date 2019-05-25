/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */


using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
	/// <summary>
	/// Represents a slice of a MemoryLane allocated via Marshal.AllocHGlobal
	/// </summary>
	public class MarshalLaneFragment : MemoryFragment
	{
		internal MarshalLaneFragment(int startIdx, int length, IntPtr lPtr, MarshalLane lane, Action dtor) : base(lane, dtor)
		{
			if (startIdx < 0 || length < 0) throw new ArgumentOutOfRangeException("startIdx or length");
			if (lPtr == null) throw new ArgumentOutOfRangeException("plane");

			this.length = length;
			StartIdx = startIdx;
			lanePtr = lPtr;
		}

		/// <summary>
		/// Writes the bytes in data (0-length) to the underlying native memory region starting at <c>offset</c> position.
		/// </summary>
		/// <param name="data">The source array.</param>
		/// <param name="offset">The writing position in the native fragment</param>
		/// <param name="length">How many bytes from the source to take.</param>
		/// <returns>The offset + length</returns>
		/// <exception cref="System.ArgumentNullException">data</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">For offset and length.</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle,
		/// AttemptToAccessDisposedLane,
		/// AttemptToAccessClosedLane
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int Write(byte[] data, int offset, int length)
		{
			LaneCheck();

			if (data == null) throw new ArgumentNullException("data");
			if (length < 0 || length > data.Length) throw new ArgumentOutOfRangeException("length");
			if (offset < 0 || offset + length > Length) throw new ArgumentOutOfRangeException("offset");

			var src = new Span<byte>(data).Slice(0, length);
			var dst = Span().Slice(offset);

			src.CopyTo(dst);

			return offset + length;
		}

		/// <summary>
		/// Reads bytes from native memory starting at offset (within the fragment region) and reads until 
		/// <c>destination</c> is full or there is no more data.
		/// The writing in <c>destination</c> starts at destOffset.
		/// </summary>
		/// <param name="destination">The read data.</param>
		/// <param name="offset">The position in source to begin reading from.</param>
		/// <param name="destOffset">Index in destination where the copying will begin at. By default is 0.</param>
		/// <returns>The total bytes read, i.e. the new offset.</returns>
		/// <exception cref="System.ArgumentNullException">If destination is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">For offset and destOffset.</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle,
		/// AttemptToAccessDisposedLane,
		/// AttemptToAccessClosedLane
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int Read(byte[] destination, int offset, int destOffset = 0)
		{
			LaneCheck();

			if (destination == null) throw new ArgumentNullException("destination");
			if (offset < 0 || offset >= Length) throw new ArgumentOutOfRangeException("offset");
			if (destOffset < 0 || destOffset >= destination.Length) throw new ArgumentOutOfRangeException("destOffset");

			var destLength = destination.Length - destOffset;
			var readLength = (destLength + offset) > Length ? Length - offset : destLength;

			var src = Span().Slice(offset, readLength);
			var dst = new Span<byte>(destination).Slice(destOffset);

			src.CopyTo(dst);

			return offset + readLength;
		}

		/// <summary>
		/// Creates a Span from a raw pointer marking the beginning of the fragment window.
		/// </summary>
		/// <returns>A Span structure</returns>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle,
		/// AttemptToAccessDisposedLane,
		/// AttemptToAccessClosedLane
		/// </exception>
		public override unsafe Span<byte> Span()
		{
			LaneCheck();

			byte* p = (byte*)lanePtr;
			p += StartIdx;
			return new Span<byte>(p, Length);
		}

		public override StorageType Type => StorageType.NativeHeapLane;

		/// <summary>
		/// Does not implement a finalizer because the resource is held by the lane.
		/// </summary>
		public override void Dispose()
		{
			if (Interlocked.CompareExchange(ref isDisposed, 1, 0) == 0)
			{
				Destructor();
				lane = null;
				lanePtr = IntPtr.Zero;
			}
		}

		/// <summary>
		/// Guards against accessing a disposed, closed or reset lane.
		/// The default is true.
		/// </summary>
		public new bool UseAccessChecks
		{
			get => useAccessChecks;
			set { useAccessChecks = value; }
		}
		/// <summary>
		/// The beginning position within the MarshalLane
		/// </summary>
		public readonly int StartIdx;
		public override int Length => length;

		/// <summary>
		/// The length of the fragment. 
		/// </summary>
		protected readonly int length;

		IntPtr lanePtr;
	}
}
