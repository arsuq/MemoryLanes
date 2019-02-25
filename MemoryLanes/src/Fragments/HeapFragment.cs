/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;

namespace System
{
	/// <summary>
	/// Represents a slice of a heap allocated buffer
	/// </summary>
	public class HeapFragment : MemoryFragment
	{
		internal HeapFragment(Memory<byte> m, HeapLane lane, Action dtor) : base(lane, dtor)
		{
			Memory = m;
		}

		/// <summary>Writes the bytes in data into the heap fragment.</summary>
		/// <param name="data">The bytes to be written</param>
		/// <param name="offset">The writing position in the fragment window.</param>
		/// <param name="length">The amount of bytes to take from <c>data</c> (0-length).</param>
		/// <returns>The new offset, i.e. <c>offset + length</c>.</returns>
		/// <exception cref="System.ArgumentNullException">If data is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If offset and length are out of range.</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int Write(byte[] data, int offset, int length)
		{
			LaneCheck();

			if (data == null) throw new ArgumentNullException("data");
			if (length < 0 || length > data.Length) throw new ArgumentOutOfRangeException("length");
			if (offset < 0 || offset + length > Memory.Length) throw new ArgumentOutOfRangeException("offset");

			var src = new Memory<byte>(data).Slice(0, length);
			var dst = Memory.Slice(offset);

			src.CopyTo(dst);

			return offset + length;
		}

		/// <summary>
		/// Reads bytes from a MMF starting at offset and reading as long as <c>destination</c> is not filled up. 
		/// The writing starts at destOffset and ends either at destination.Length or at fragment.Length - offset.
		/// </summary>
		/// <param name="destination">The buffer where the MMF data goes to.</param>
		/// <param name="offset">The reading starts at offset.</param>
		/// <param name="destOffset">The writing starts at destOffset.</param>
		/// <returns>The new reading position = offset + the read bytes notNullsCount.</returns>
		/// <exception cref="System.ArgumentNullException">If destination is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">For offset and destOffset.</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int Read(byte[] destination, int offset, int destOffset = 0)
		{
			LaneCheck();

			if (destination == null) throw new ArgumentNullException("destination");
			if (offset < 0 || offset >= Memory.Length) throw new ArgumentOutOfRangeException("offset");
			if (destOffset < 0 || destOffset >= destination.Length) throw new ArgumentOutOfRangeException("destOffset");

			var destLength = destination.Length - destOffset;
			var readLength = (destLength + offset) > Memory.Length ? Memory.Length - offset : destLength;

			var src = Memory.Slice(offset, readLength);
			var dst = new Memory<byte>(destination).Slice(destOffset);

			src.CopyTo(dst);

			return offset + readLength;
		}

		public override StorageType Type => StorageType.ManagedHeapLane;

		public override void Dispose()
		{
			if (!isDisposed)
			{
				Destructor();
				lane = null;
				Memory = null;
				isDisposed = true;
			}
		}

		/// <summary>
		/// Guards against accessing a disposed, closed or reset lane.
		/// The default is true.
		/// </summary>
		public bool UseAccessChecks
		{
			get => useAccessChecks;
			set { useAccessChecks = value; }
		}

		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		public override Span<byte> Span()
		{
			LaneCheck();
			return Memory.Span;
		}

		/// <summary>
		/// Gets the fragment Memory. 
		/// </summary>
		/// <param name="f">The fragment.</param>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		public static implicit operator Memory<byte>(HeapFragment f)
		{
			f.LaneCheck();
			return f.Memory;
		}

		/// <summary>
		/// Casts the fragment as ReadOnlyMemory of bytes. 
		/// </summary>
		/// <param name="f">The fragment.</param>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		public static implicit operator ReadOnlyMemory<byte>(HeapFragment f)
		{
			f.LaneCheck();
			return f.Memory;
		}

		public Memory<byte> Memory;
		public override int Length => Memory.Length;
	}
}