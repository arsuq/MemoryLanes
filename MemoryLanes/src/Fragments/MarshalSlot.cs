/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
	/// <summary>
	/// Represents a slice of unmanaged memory. 
	/// This object is not part of a MemoryLane and its lifetime does not affect other MemoryFragment instances.
	/// Use the MarshalSlot for large and/or long living data, which would fragment the memory lanes 
	/// by preventing reset, or burden the GC and the managed heap if allocated there.
	/// </summary>
	/// <remarks>
	/// This class has multiple accessors with different meaning and one should be careful not to mix the Read/Writes 
	/// with Span() and the Store/Load/Reserve methods.
	/// </remarks>
	public class MarshalSlot : MemoryFragment
	{
		public MarshalSlot(int length)
		{
			if (length < 0) throw new ArgumentOutOfRangeException("length");

			this.length = length;
			slotPtr = Marshal.AllocHGlobal(length);
		}

		/// <summary>
		/// Take a Span of the whole range.
		/// </summary>
		/// <param name="format">Zero the bytes</param>
		/// <returns>The Span</returns>
		public unsafe Span<byte> Span(bool format = false)
		{
			var span = new Span<byte>((byte*)slotPtr, Length);
			if (format) for (var i = 0; i < Length; i++) span[i] = 0;

			return span;
		}

		/// <summary>
		/// Writes the bytes in data (0-length) to the underlying native memory region starting at <c>offset</c> position.
		/// </summary>
		/// <param name="data">The source array.</param>
		/// <param name="offset">The writing position in the native fragment</param>
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
		/// Store an unmanaged structure which has no managed references. 
		/// </summary>
		/// <typeparam name="T">Unmanaged structure type</typeparam>
		/// <param name="str">The value</param>
		/// <returns></returns>
		public unsafe static MarshalSlot Store<T>(T str) where T : unmanaged
		{
			var mps = new MarshalSlot(sizeof(T));
			var p = (T*)mps.slotPtr.ToPointer();
			*p = str;
			return mps;
		}

		/// <summary>
		/// Returns a pointer to a structure, allocated in unmanaged memory.
		/// </summary>
		/// <typeparam name="T">A ref-free type.</typeparam>
		/// <param name="mps">The pointer holder.</param>
		/// <returns></returns>
		public unsafe static T* Reserve<T>(out MarshalSlot mps) where T : unmanaged
		{
			mps = new MarshalSlot(sizeof(T));
			var p = (T*)mps.slotPtr.ToPointer();
			return p;
		}

		/// <summary>
		/// Constructs previously stored unmanaged structure.
		/// </summary>
		/// <typeparam name="T">A reference free structure type</typeparam>
		/// <returns>The initialized structure</returns>
		public unsafe T Load<T>() where T : unmanaged
		{
			if (slotPtr == IntPtr.Zero) throw new ArgumentException("slotPtr");
			var p = (T*)slotPtr.ToPointer();
			return *p;
		}

		public override Span<byte> Span() => Span(false);

		public override StorageType Type => StorageType.NativeHeapSlot;

		/// <summary>
		/// Does not implement a finalizer!
		/// </summary>
		public override void Dispose()
		{
			if (!isDisposed)
			{
				if (slotPtr != IntPtr.Zero)
				{
					Marshal.FreeHGlobal(slotPtr);
					slotPtr = IntPtr.Zero;
				}
				isDisposed = true;
			}
		}

		public override int Length => length;

		readonly int length;
		IntPtr slotPtr;
	}
}
