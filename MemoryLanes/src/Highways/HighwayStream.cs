using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MemoryLanes.src.Highways
{
	public class HighwayStream : Stream
	{
		public HighwayStream(IMemoryHighway hw)
		{
			Highway = hw ?? throw new ArgumentNullException();

		}

		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => true;

		public override long Length => throw new NotImplementedException();

		public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public override void Flush()
		{
			throw new NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public readonly IMemoryHighway Highway;
	}
}
