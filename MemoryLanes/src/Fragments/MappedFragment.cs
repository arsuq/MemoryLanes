using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace System
{
	/// <summary>
	/// Represents a fragment of a memory mapped file.
	/// </summary>
	public class MappedFragment : MemoryFragment
	{
		public MappedFragment() { }

		public MappedFragment(long startIdx, int length, MemoryMappedViewAccessor va, Action dtor)
		{
			StartIdx = startIdx;
			this.length = length;
			destructor = dtor;
			mmva = va;
		}

		/// <summary>Writes the bytes in data into the MMF.</summary>
		/// <remarks>Note that the Read() and Write() methods are not synchronized with access trough Span().
		/// Use either Read/Write or Span().</remarks>
		/// <param name="data">The bytes to be written</param>
		/// <param name="offset">The number of written bytes so far.</param>
		/// <param name="length">The amount of bytes to take from data (takes from 0 to length).</param>
		/// <returns>The total written bytes, i.e. offset + length.</returns>
		/// <exception cref="System.ArgumentNullException">If data is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If offset and length are out of range.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int Write(byte[] data, int offset, int length)
		{
			if (data == null) throw new ArgumentNullException("data");
			if (offset < 0 || offset > Length) throw new ArgumentOutOfRangeException("offset");
			if (length < 0 || length > Length || length > data.Length) throw new ArgumentOutOfRangeException("length");
			if (length + offset > Length) throw new ArgumentOutOfRangeException("length or/and offset", "The length + offset > capacity.");

			mmva.WriteArray(StartIdx + offset, data, 0, length);

			return offset + length;
		}

		/// <summary>
		/// Reads bytes from a MMF starting at offset and reading as long as <c>destination</c> is not filled up. 
		/// The writing starts at destOffset and ends either at destination.Length or at fragment.Length - offset.
		/// </summary>
		/// <param name="destination">The buffer where the MMF data goes to.</param>
		/// <param name="offset">The total read bytes so far.</param>
		/// <param name="destOffset">Index in destination where the copying will begin at. By default is 0.</param>
		/// <returns>The total bytes read from the MMF, i.e. the new offset.</returns>
		/// <exception cref="System.ArgumentNullException">If destination is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">For offset and destOffset.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int Read(byte[] destination, int offset, int destOffset = 0)
		{
			if (destination == null) throw new ArgumentNullException("destination");
			if (offset < 0 || offset >= Length) throw new ArgumentOutOfRangeException("offset");
			if (destOffset < 0 || destOffset > destination.Length) throw new ArgumentOutOfRangeException("destOffset");

			var destLength = destination.Length - destOffset;
			var readLength = (destLength + offset) > Length ? Length - offset : destLength;

			return offset + mmva.ReadArray(StartIdx + offset, destination, destOffset, readLength);
		}

		/// <summary>
		/// Creates a Span from a raw pointer which marks the beginning of the 
		/// MemoryMappedViewAccessor window.
		/// </summary>
		/// <returns>A Span structure</returns>
		public override unsafe Span<byte> Span()
		{
			byte* p = null;
			try
			{
				mmva.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
				return new Span<byte>(p, Length);
			}
			finally
			{
				if (p != null)
					mmva.SafeMemoryMappedViewHandle.ReleasePointer();
			}
		}

		public override void Dispose() => destroy();

		void destroy(bool isGC = false)
		{
			if (destructor != null)
			{
				destructor();
				destructor = null;
				mmva = null;
				if (!isGC) GC.SuppressFinalize(this);
			}
		}

		~MappedFragment() => destroy(true);

		/// <summary>
		/// The byte offset in the MMF where the fragment starts.
		/// </summary>
		public readonly long StartIdx;
		/// <summary>
		/// The length of the fragment. 
		/// </summary>
		protected readonly int length;

		public override int Length => length;

		Action destructor;
		MemoryMappedViewAccessor mmva;
	}
}
