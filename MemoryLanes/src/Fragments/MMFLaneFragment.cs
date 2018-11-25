using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace System
{
	public struct MMFFragment : IDisposable
	{
		public MMFFragment(long startIdx, int length, MemoryMappedViewAccessor va, Action dtor)
		{
			StartIdx = startIdx;
			Length = length;
			destructor = dtor;
			mmva = va;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(byte[] data, int offset, int length)
		{
			if (data == null) throw new NullReferenceException("data");
			if (length < 0 || length > Length || length > data.Length)
				throw new MemoryLaneException(
					MemoryLaneException.Code.MappedFileRWOutOfBounds,
					"Attempt to write outside the given range.");

			var writeLength = (length + offset) > Length ?
				 Length - offset : length;

			mmva.WriteArray(StartIdx + offset, data, 0, writeLength);

			return offset + writeLength;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(byte[] destinaton, int offset)
		{
			if (destinaton == null || offset < 0 || offset >= Length)
				throw new MemoryLaneException(
					MemoryLaneException.Code.MissingOrInvalidArgument,
					"Missing or too short destination buffer.");

			var readLength = (destinaton.Length + offset) > Length ?
				Length - offset : destinaton.Length;

			return mmva.ReadArray(StartIdx + offset, destinaton, 0, Length);
		}

		public unsafe Span<byte> Span()
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

		public void Dispose()
		{
			if (destructor != null)
			{
				destructor();
				destructor = null;
				mmva = null;
			}
		}

		public readonly long StartIdx;
		public readonly int Length;

		Action destructor;
		MemoryMappedViewAccessor mmva;
	}
}
