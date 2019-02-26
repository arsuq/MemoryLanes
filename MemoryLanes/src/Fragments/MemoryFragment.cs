/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
	public abstract class MemoryFragment : IDisposable
	{
		public MemoryFragment() { }

		public MemoryFragment(MemoryLane lane, Action dtor)
		{
			this.lane = lane ?? throw new ArgumentNullException("lane");
			Destructor = dtor ?? throw new ArgumentNullException("dtor");
			laneCycle = lane.LaneCycle;
		}

		/// <summary>
		/// Gets or sets a byte at index.
		/// </summary>
		/// <param name="index">The index</param>
		/// <returns>The value at index</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If index is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		public byte this[int index]
		{
			get
			{
				if (index < 0 || index > Length)
					throw new ArgumentOutOfRangeException("index");

				LaneCheck();

				return Span()[index];
			}
			set
			{
				if (index < 0 || index > Length)
					throw new ArgumentOutOfRangeException("index");

				LaneCheck();

				Span()[index] = value;
			}
		}

		#region Primitive Writes

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(byte v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			Span()[idx] = v;
			return idx + 1;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(bool v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v) ? idx + 1 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(char v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v) ? idx + 2 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(short v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v) ? idx + 2 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(int v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v) ? idx + 4 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(uint v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v) ? idx + 4 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(long v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v) ? idx + 8 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(ulong v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v) ? idx + 8 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(double v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v) ? idx + 8 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(Guid v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return v.TryWriteBytes(Span().Slice(idx)) ? idx + 16 : -idx;
		}

		/// <summary>
		/// Writes the value starting at idx.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the value length in bytes. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(DateTime v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(idx), v.ToBinary()) ? idx + 8 : -idx;
		}

		/// <summary>
		/// Copies the span starting at idx.
		/// </summary>
		/// <param name="bytes">The span to be copied</param>
		/// <param name="idx">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. idx + the span length. If fails returns -idx.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(Span<byte> bytes, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			return bytes.TryCopyTo(Span().Slice(idx)) ? idx + bytes.Length : -idx;
		}

		#endregion

		#region Primitive Reads

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref byte v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = Span()[idx];

			return idx + 1;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref bool v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = BitConverter.ToBoolean(Span().Slice(idx, 1));

			return idx + 1;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref char v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = BitConverter.ToChar(Span().Slice(idx, 2));

			return idx + 2;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref short v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = BitConverter.ToInt16(Span().Slice(idx, 2));

			return idx + 2;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref int v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = BitConverter.ToInt32(Span().Slice(idx, 4));

			return idx + 4;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref uint v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = BitConverter.ToUInt32(Span().Slice(idx, 4));

			return idx + 4;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref long v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = BitConverter.ToInt64(Span().Slice(idx, 8));

			return idx + 8;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref ulong v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = BitConverter.ToUInt64(Span().Slice(idx, 8));

			return idx + 8;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref double v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = BitConverter.ToDouble(Span().Slice(idx, 8));

			return idx + 8;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref Guid v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = new Guid(Span().Slice(idx, 16));

			return idx + 16;
		}

		/// <summary>
		/// Reads the value starting at idx.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="idx">Index in the fragment window.</param>
		/// <returns>The updated position as idx + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If idx is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref DateTime v, int idx)
		{
			if (idx < 0 || idx >= Length)
				throw new ArgumentOutOfRangeException("idx");

			LaneCheck();
			v = DateTime.FromBinary(BitConverter.ToInt64(Span().Slice(idx, 8)));

			return idx + 8;
		}

		#endregion

		/// <summary>Writes the bytes in data into the fragment storage.</summary>
		/// <param name="data">The bytes to be written</param>
		/// <param name="offset">The writing position in the fragment window.</param>
		/// <param name="length">The amount of bytes to take from <c>data</c> (0-length).</param>
		/// <returns>The new offset, i.e. <c>offset + length</c>.</returns>
		/// <exception cref="System.ArgumentNullException">If data is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If offset and length are out of range.</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		public abstract int Write(byte[] data, int offset, int length);

		/// <summary>
		/// Reads bytes from the fragment storage starting at offset and reading as long as <c>destination</c> is not filled up. 
		/// The writing starts at destOffset and ends either at destination.Length or at fragment.Length - offset.
		/// </summary>
		/// <param name="destination">The read buffer.</param>
		/// <param name="offset">The reading starts at offset.</param>
		/// <param name="destOffset">The writing starts at destOffset.</param>
		/// <returns>The new reading position = offset + the read bytes notNullsCount.</returns>
		/// <exception cref="System.ArgumentNullException">If destination is null.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">For offset and destOffset.</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		public abstract int Read(byte[] destination, int offset, int destOffset = 0);

		public abstract Span<byte> Span();
		public abstract int Length { get; }
		public abstract void Dispose();

		/// <summary>
		/// The fragment memory type.
		/// </summary>
		public abstract StorageType Type { get; }

		/// <summary>
		/// Casts the fragment bytes as a span of T
		/// </summary>
		/// <typeparam name="T">The casting struct</typeparam>
		/// <returns>A span of structs</returns>
		public Span<T> ToSpan<T>() where T : struct => MemoryMarshal.Cast<byte, T>(Span());

		/// <summary>
		/// Creates a stream with the fragment as a storage.
		/// </summary>
		/// <returns>The fragment stream</returns>
		public FragmentStream CreateStream() => new FragmentStream(this);

		/// <summary>
		/// Creates new byte array and copies the data into it.
		/// </summary>
		/// <returns></returns>
		public byte[] ToArray()
		{
			var b = new byte[Length];
			Span().CopyTo(b);

			return b;
		}

		public bool IsDisposed => isDisposed;
		public int LaneCycle => laneCycle;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void LaneCheck()
		{
			if (!useAccessChecks) return;
			if (LaneCycle != Lane.LaneCycle) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessWrongLaneCycle);
			if (Lane.IsClosed) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessClosedLane);
			if (Lane.IsDisposed) throw new MemoryLaneException(MemoryLaneException.Code.AttemptToAccessDisposedLane);
		}

		/// <summary>
		/// If true checks whether the lane is closed, disposed or the cycle has not changed.
		/// Derived classes may expose it as public, by default is true.
		/// </summary>
		protected bool useAccessChecks = true;

		internal bool UseAccessChecks
		{
			get => useAccessChecks;
			set => useAccessChecks = value;
		}

		/// <summary>
		/// Gets the fragment Span. 
		/// </summary>
		/// <param name="f">The fragment.</param>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		public static implicit operator Span<byte>(MemoryFragment f)
		{
			f.LaneCheck();
			return f.Span();
		}

		/// <summary>
		/// Casts the fragment as ReadOnlySpan of bytes. 
		/// </summary>
		/// <param name="f">The fragment.</param>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		public static implicit operator ReadOnlySpan<byte>(MemoryFragment f)
		{
			f.LaneCheck();
			return f.Span();
		}

		public MemoryLane Lane => lane;
		protected MemoryLane lane;
		protected readonly int laneCycle;
		protected bool isDisposed;
		protected readonly Action Destructor;
	}
}
