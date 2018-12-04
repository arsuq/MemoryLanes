using System;
using System.Collections.Generic;
using System.Text;

namespace System
{
	/// <summary>
	/// Represents the boxed interface of a fragment structure.
	/// </summary>
	public interface IMemoryLaneFragment : IDisposable
	{
		int Write(byte[] data, int offset, int length);
		int Read(byte[] destination, int offset, int destOffset = 0);
		Span<byte> Span();
		int Length { get; }
	}
}
