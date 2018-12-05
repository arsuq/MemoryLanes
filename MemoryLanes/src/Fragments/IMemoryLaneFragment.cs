using System;
using System.Collections.Generic;
using System.Text;

namespace System
{
	public abstract class MemoryLaneFragment : IDisposable
	{
		public abstract int Write(byte[] data, int offset, int length);
		public abstract int Read(byte[] destination, int offset, int destOffset = 0);
		public abstract Span<byte> Span();
		public abstract int Length { get; }
		public abstract void Dispose();
	}
}
