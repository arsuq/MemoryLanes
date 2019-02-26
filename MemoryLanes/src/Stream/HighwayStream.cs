/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace System.IO
{
	/// <summary>
	/// A stream with a MemoryHighway for storage.
	/// </summary>
	public class HighwayStream : Stream, IDisposable
	{
		/// <summary>
		/// Creates a new Highway stream instance.
		/// </summary>
		/// <param name="hw">The highway to use for storage.</param>
		/// <param name="fragmentSize">The size of the fragments.</param>
		public HighwayStream(IMemoryHighway hw, int fragmentSize)
		{
			Highway = hw ?? throw new ArgumentNullException();
			if (fragmentSize < 1) throw new ArgumentOutOfRangeException("fragmentSize");

			FragmentSize = fragmentSize;
			AllocTimeoutMS = -1;
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
			if (isDisposed) throw new ObjectDisposedException("HighwayStream");
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset");
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			if (buffer.Length - offset < count) count = buffer.Length - offset;
			if (count > len - pos) count = (int)(len - pos);

			int fsi = -1;
			int fei = -1;
			int so = -1;
			int eo = -1;
			int p = (int)pos;
			int wp = offset;

			locate(p, ref fsi, ref so);
			locate(p + count, ref fei, ref eo);

			Span<byte> s;
			Span<byte> t;

			// In the same fragment
			if (fsi == fei)
			{
				s = fragments[fsi].Span().Slice(so, count);
				t = buffer.AsSpan(offset, count);
				s.CopyTo(t);
				wp += count;
				pos += count;
			}
			else
			{
				// From the start offset to the end of the fragment
				if (so < FragmentSize)
				{
					s = fragments[fsi].Span().Slice(so);
					t = buffer.AsSpan(offset, s.Length);
					s.CopyTo(t);
					wp += s.Length;
					pos += s.Length;
				}

				// All from the middle ones
				for (int i = fsi + 1; i < fei; i++)
				{
					s = fragments[i].Span();
					t = buffer.AsSpan(wp, s.Length);
					s.CopyTo(t);
					wp += s.Length;
					pos += s.Length;
				}

				// From 0 to the end offset from the last one
				if (eo > 0)
				{
					s = fragments[fei].Span().Slice(0, eo);
					t = buffer.AsSpan(wp, s.Length);
					s.CopyTo(t);
					wp += s.Length;
					pos += s.Length;
				}
			}

			return wp - offset;
		}

		/// <summary>
		/// Moves the position.
		/// </summary>
		/// <returns>The new position.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			if (isDisposed) throw new ObjectDisposedException("HighwayStream");

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
					var npos = len + offset;
					if (npos < 0 || npos >= len) throw new ArgumentOutOfRangeException();
					pos = npos;
					break;
				}
				default: throw new ArgumentException();
			}

			return pos;
		}

		/// <summary>
		/// If the value is greater than Length allocates more fragments,
		/// otherwise disposes the unnecessary ones.
		/// </summary>
		/// <param name="value">The desired length.</param>
		public override void SetLength(long value)
		{
			if (isDisposed) throw new ObjectDisposedException("HighwayStream");
			if (value < 0) throw new ArgumentOutOfRangeException();

			var available = fragments.Count * FragmentSize;

			if (available < value)
				while (available < value)
				{
					var f = Highway.AllocFragment(FragmentSize, AllocTimeoutMS);
					fragments.Add(f);
					available += FragmentSize;
				}
			else
				while (available > value)
				{
					fragments[fragments.Count - 1].Dispose();
					fragments.RemoveAt(fragments.Count - 1);
					available -= FragmentSize;
				}

			len = (int)value;
		}

		/// <summary>
		/// Writes the buffer slice from offset to offset + count into the stream.
		/// Unlike Read doesn't cut the count but throws. 
		/// Allocates more fragments if there is not enough space.
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
			if (isDisposed) throw new ObjectDisposedException("HighwayStream");
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset");
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			if (buffer.Length - offset < count) throw new ArgumentException("count");

			int fsi = -1;
			int fei = -1;
			int so = -1;
			int eo = -1;
			int p = (int)pos;
			int wp = offset;

			locate(p, ref fsi, ref so);
			locate(p + count, ref fei, ref eo);

			Span<byte> s;
			Span<byte> t;

			if (pos + count > len) SetLength(pos + count);

			if (fsi == fei)
			{
				t = fragments[fsi].Span().Slice(so, count);
				s = buffer.AsSpan(offset, count);
				s.CopyTo(t);
				pos += count;
			}
			else
			{
				if (so < FragmentSize)
				{
					t = fragments[fsi].Span().Slice(so);
					s = buffer.AsSpan(offset, t.Length);
					s.CopyTo(t);
					wp += s.Length;
					pos += s.Length;
				}

				for (int i = fsi + 1; i < fei; i++)
				{
					t = fragments[i].Span();
					s = buffer.AsSpan(wp, t.Length);
					s.CopyTo(t);
					wp += s.Length;
					pos += s.Length;
				}

				if (eo > 0)
				{
					t = fragments[fei].Span().Slice(0, eo);
					s = buffer.AsSpan(wp, t.Length);
					s.CopyTo(t);
					pos += t.Length;
				}
			}
		}

		/// <summary>
		/// Does not dispose the highway, just the allocated fragments.
		/// </summary>
		public void IDispose()
		{
			if (!isDisposed)
			{
				try
				{
					foreach (var f in fragments)
						if (f != null && !f.IsDisposed)
							f.Dispose();

					fragments = null;
					base.Dispose();
				}
				catch { }
				isDisposed = true;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		void locate(int index, ref int fragIdx, ref int offset)
		{
			if (index < FragmentSize)
			{
				fragIdx = 0;
				offset = index;
			}
			else
			{
				fragIdx = index / FragmentSize;
				offset = index % FragmentSize;
			}
		}

		/// <summary>
		/// The lanes allocation timeout in milliseconds 
		/// </summary>
		public int AllocTimeoutMS { get; set; }

		public readonly int FragmentSize;
		public readonly IMemoryHighway Highway;

		long pos;
		int len;
		bool isDisposed;
		List<MemoryFragment> fragments = new List<MemoryFragment>();
	}
}
