using System.Runtime.InteropServices;

namespace System
{
	public abstract class MemoryFragment : IDisposable
	{
		public abstract int Write(byte[] data, int offset, int length);
		public abstract int Read(byte[] destination, int offset, int destOffset = 0);
		public abstract Span<byte> Span();
		public abstract int Length { get; }
		public abstract long LaneCycle { get; }
		public abstract void Dispose();

		public Span<T> ToSpan<T>() where T : struct =>
			MemoryMarshal.Cast<byte, T>(Span());
	}
}
