
using System.Runtime.CompilerServices;

namespace System
{
	public struct LOHFragment : IMemoryLaneFragment
	{
		public LOHFragment(Memory<byte> m, Action dtor)
		{
			if (dtor == null) throw new NullReferenceException("dtor");

			Memory = m;
			destructor = dtor;
		}

		/// <summary>Writes the bytes in data into the heap fragment.</summary>
		/// <param name="data">The bytes to be written</param>
		/// <param name="offset">The writing position in the fragment window.</param>
		/// <param name="length">The amount of bytes to take from <c>data</c> (0-length).</param>
		/// <returns>The new offset, i.e. <c>offset + length</c>.</returns>
		/// <exception cref="System.ArgumentNullException">If data is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If offset and length are out of range.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(byte[] data, int offset, int length)
		{
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
		/// <returns>The new reading position = offset + the read bytes count.</returns>
		/// <exception cref="System.ArgumentNullException">If destination is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">For offset and destOffset.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(byte[] destination, int offset, int destOffset = 0)
		{
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

		public void Dispose()
		{
			if (destructor != null)
			{
				destructor();
				destructor = null;
				Memory = null;
			}
		}

		public Memory<byte> Memory;
		public Span<byte> Span() => Memory.Span;
		public int Length => Memory.Length;

		Action destructor;
	}
}