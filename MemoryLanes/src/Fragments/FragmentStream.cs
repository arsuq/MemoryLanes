namespace System.IO
{
	/// <summary>
	/// A stream object with a fragment as a memory storage.
	/// </summary>
	public class FragmentStream : Stream
	{
		/// <summary>
		/// Creates a stream objects.
		/// </summary>
		/// <param name="frag"></param>
		/// <exception cref="ArgumentNullException">If frag is null.</exception>
		/// <exception cref="ObjectDisposedException">If the fragment is disposed.</exception>
		public FragmentStream(MemoryFragment frag)
		{
			if (frag.IsDisposed) throw new ObjectDisposedException("frag");
			Fragment = frag ?? throw new ArgumentNullException("frag");
			len = frag.Length;
		}

		/// <summary>
		/// True.
		/// </summary>
		public override bool CanRead => true;
		/// <summary>
		/// True.
		/// </summary>
		public override bool CanSeek => true;
		/// <summary>
		/// True.
		/// </summary>
		public override bool CanWrite => true;
		/// <summary>
		/// The current length.
		/// </summary>
		public override long Length => len;
		/// <summary>
		/// Get set the position.
		/// </summary>
		public override long Position
		{
			get => pos;
			set
			{
				if (value < 0 || value >= len)
					throw new ArgumentOutOfRangeException();

				Seek(value, SeekOrigin.Begin);
			}
		}

		/// <summary>
		/// Does nothing.
		/// </summary>
		public override void Flush() { }

		/// <summary>
		/// The standard stream Read. 
		/// The count could be cut twice: 
		/// First to buffer.Length - offset if the buffer slice from offset is smaller than count.
		/// Second to stream's Length - Position if count reaches beyond the capacity.
		/// </summary>
		/// <param name="buffer">Where to write.</param>
		/// <param name="offset">Start index in buffer.</param>
		/// <param name="count">Number of bytes to read.</param>
		/// <returns>The number of read bytes.</returns>
		/// <exception cref="ArgumentNullException">If buffer is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If offset or count are negative.</exception>
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset");
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			if (buffer.Length - offset < count) count = buffer.Length - offset;
			if (count > len - pos) count = (int)(len - pos);

			Fragment.LaneCheck();

			var s = Fragment.Span().Slice((int)pos, count);
			var t = buffer.AsSpan(offset);
			pos += count;

			s.CopyTo(t);

			return count;
		}

		/// <summary>
		/// Writes the buffer slice from offset to offset + count into the stream.
		/// Unlike Read doesn't cut the count but throws because it's void.
		/// </summary>
		/// <param name="buffer">The source bytes.</param>
		/// <param name="offset">Starting index in buffer.</param>
		/// <param name="count">Number of bytes to take.</param>
		/// <exception cref="ArgumentNullException">If buffer is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// If offset or count are negative or count reaches beyond the fragment capacity.</exception>
		/// <exception cref="ArgumentException">If the buffer slice from offset is smaller than count.</exception>
		public override void Write(byte[] buffer, int offset, int count)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset");
			if (count < 0 || pos + count > Fragment.Length) throw new ArgumentOutOfRangeException("count");
			if (buffer.Length - offset < count) throw new ArgumentException("count");

			Fragment.LaneCheck();

			var t = buffer.AsSpan(offset, count);
			Fragment.Write(t, (int)pos);
			pos += count;
		}

		/// <summary>
		/// Moves the current position.
		/// </summary>
		/// <param name="offset">Number of bytes.</param>
		/// <param name="origin">Use negative numbers with SeekOrigin.End</param>
		/// <returns>The Position value.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
				{
					if (offset < 0 || offset >= len) throw new ArgumentOutOfRangeException();
					pos = offset;
					break;
				}
				case SeekOrigin.Current:
				{
					var npos = pos + offset;
					if (npos < 0 || npos >= len) throw new ArgumentOutOfRangeException();
					pos = npos;
					break;
				}
				case SeekOrigin.End:
				{
					var npos = Fragment.Length + offset;
					if (npos < 0 || npos >= len) throw new ArgumentOutOfRangeException();
					pos = npos;
					break;
				}
				default: throw new ArgumentException();
			}

			return pos;
		}

		/// <summary>
		/// Will shrink the stream length, but not the fragment.
		/// </summary>
		/// <param name="value"></param>
		public override void SetLength(long value)
		{
			if (value < 0 || value >= Fragment.Length) throw new ArgumentOutOfRangeException();

			len = value;
		}

		public readonly MemoryFragment Fragment;
		long pos;
		long len;
	}
}
