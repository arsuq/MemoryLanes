
using System.Runtime.CompilerServices;

namespace System
{
	/// <summary>
	/// Represents a slice of a MemoryLane allocated via Marshal.AllocHGlobal
	/// </summary>
	public class MarshalLaneFragment : MemoryFragment
	{
		public MarshalLaneFragment() { }

		public MarshalLaneFragment(int startIdx, int length, IntPtr lane, Action dtor)
		{
			if (startIdx < 0 || length < 0) throw new ArgumentOutOfRangeException("startIdx or length");
			if (dtor == null) throw new NullReferenceException("dtor");
			if (lane == null) throw new NullReferenceException("lane");

			StartIdx = startIdx;
			this.length = length;
			destructor = dtor;
			lanePtr = lane;
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
		/// Creates a Span from a raw pointer marking the beginning of the fragment window.
		/// </summary>
		/// <returns>A Span structure</returns>
		public override unsafe Span<byte> Span()
		{
			byte* p = (byte*)lanePtr;
			p += StartIdx;
			return new Span<byte>(p, Length);
		}

		public override void Dispose() => destroy();

		void destroy(bool isGC = false)
		{
			if (destructor != null)
			{
				destructor();
				destructor = null;
				lanePtr = IntPtr.Zero;
				if (!isGC) GC.SuppressFinalize(this);
			}
		}

		~MarshalLaneFragment() => destroy(true);

		/// <summary>
		/// The beginning position within the MarshalLane
		/// </summary>
		public readonly int StartIdx;
		/// <summary>
		/// The length of the fragment. 
		/// </summary>
		protected readonly int length;

		public override int Length => length;

		Action destructor;
		IntPtr lanePtr;
	}
}
