/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
	/// <summary>
	/// Represents a slice of managed memory. 
	/// This object is not part of a MemoryLane and its lifetime does not affect other MemoryFragment instances.
	/// </summary>
	public class HeapSlot : MemoryFragment
	{
		/// <summary>
		/// Creates a MemoryFragment from a managed array. 
		/// Be aware that the buffer could potentially be mutated concurrently or 
		/// unexpectedly nulled from outside. Consider using the length constructor.
		/// </summary>
		/// <param name="slot">The buffer is shared!</param>
		/// <exception cref="ArgumentNullException"></exception>
		public HeapSlot(byte[] slot)
		{
			if (slot == null) throw new ArgumentNullException("slot");

			useAccessChecks = false;
			this.slot = slot;
			this.length = slot.Length;
		}

		/// <summary>
		/// Creates a MemoryFragment from a managed array. 
		/// Be aware that the buffer could potentially be mutated concurrently or 
		/// unexpectedly nulled from outside. Consider using the length constructor.
		/// </summary>
		/// <param name="slot">The buffer</param>
		/// <param name="from">The start index</param>
		/// <param name="length">The number of bytes</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		public HeapSlot(byte[] slot, int from, int length)
		{
			if (slot == null) throw new ArgumentNullException("slot");
			if (length < 1 || length > slot.Length) throw new ArgumentOutOfRangeException("length");
			if (from < 0 || from > slot.Length) throw new ArgumentOutOfRangeException("from");
			if (from + length > slot.Length) throw new ArgumentOutOfRangeException("from + length");

			useAccessChecks = false;
			this.slot = slot.AsMemory(from, length);
			this.length = slot.Length;
		}

		/// <summary>
		/// Allocates a buffer on the managed heap.
		/// </summary>
		/// <param name="length">The buffer size in bytes</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public HeapSlot(int length)
		{
			if (length < 1) throw new ArgumentOutOfRangeException("length", "Invalid length");

			useAccessChecks = false;
			slot = new byte[length];
			this.length = slot.Length;
		}

		/// <summary>
		/// Take a Span of the whole range.
		/// </summary>
		/// <param name="format">Zero the bytes</param>
		/// <returns>The Span</returns>
		public Span<byte> Span(bool format = false)
		{
			if (format) for (var i = 0; i < Length; i++) slot.Span[i] = 0;

			return slot.Span;
		}

		/// <summary>
		/// Writes the bytes in data (0-length) to the slot starting at <c>offset</c> position.
		/// </summary>
		/// <param name="data">The source array.</param>
		/// <param name="offset">The writing position in the heap slot.</param>
		/// <param name="length">How many bytes from the source to take.</param>
		/// <returns>The offset + length</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int Write(byte[] data, int offset, int length)
		{
			if (data == null) throw new ArgumentNullException("data");
			if (length < 0 || length > data.Length) throw new ArgumentOutOfRangeException("length");
			if (offset < 0 || offset + length > Length) throw new ArgumentOutOfRangeException("offset");

			var src = new Span<byte>(data).Slice(0, length);
			var dst = Span().Slice(offset);

			src.CopyTo(dst);

			return offset + length;
		}

		/// <summary>
		/// Reads bytes from the slot starting at offset until  <c>destination</c> is full or there is no more data.
		/// The writing in <c>destination</c> starts at destOffset.
		/// </summary>
		/// <param name="destination">The read data.</param>
		/// <param name="offset">The position in source to begin reading from.</param>
		/// <param name="destOffset">Index in destination where the copying will begin at. By default is 0.</param>
		/// <returns>The total bytes read, i.e. the new offset.</returns>
		/// <exception cref="System.ArgumentNullException">If destination is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">For offset and destOffset.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int Read(byte[] destination, int offset, int destOffset = 0)
		{
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
		/// Makes a span over the whole slot.
		/// </summary>
		/// <returns>The span structure.</returns>
		public override Span<byte> Span() => Span(false);

		public override StorageType Type => StorageType.ManagedHeapSlot;

		public override void Dispose()
		{
			if (Interlocked.CompareExchange(ref isDisposed, 1, 0) == 0) slot = null;
		}

		/// <summary>
		/// Gets the fragment Memory. 
		/// </summary>
		/// <param name="ms">The slot.</param>
		public static implicit operator Memory<byte>(HeapSlot ms) => ms != null ? ms.slot : default;

		/// <summary>
		/// Casts the fragment as ReadOnlyMemory of bytes. 
		/// </summary>
		/// <param name="ms">The slot.</param>
		public static implicit operator ReadOnlyMemory<byte>(HeapSlot ms) => ms != null ? ms.slot : default;

		/// <summary>
		/// The fragment length.
		/// </summary>
		public override int Length => length;

		readonly int length;
		Memory<byte> slot;
	}
}
