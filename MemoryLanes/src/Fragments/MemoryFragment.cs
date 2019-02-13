/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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

				laneCheck();

				return Span()[index];
			}
			set
			{
				if (index < 0 || index > Length)
					throw new ArgumentOutOfRangeException("index");

				laneCheck();

				Span()[index] = value;
			}
		}

		#region Primitive Writes

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(byte v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 1 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(bool v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 1 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(char v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 2 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(short v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 2 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(int v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 4 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(uint v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 4 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(long v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 8 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(ulong v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 8 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(double v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v) ? startpos + 8 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(Guid v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return v.TryWriteBytes(Span().Slice(startpos)) ? startpos + 16 : -startpos;
		}

		/// <summary>
		/// Writes the value starting at startpos.
		/// </summary>
		/// <param name="v">The value</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the value length in bytes. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(DateTime v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return BitConverter.TryWriteBytes(Span().Slice(startpos), v.ToBinary()) ? startpos + 8 : -startpos;
		}

		/// <summary>
		/// Copies the span starting at startpos.
		/// </summary>
		/// <param name="bytes">The span to be copied</param>
		/// <param name="startpos">The index in the fragment window.</param>
		/// <returns>The new offset, i.e. startpos + the span length. If fails returns -startpos.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Write(Span<byte> bytes, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			return bytes.TryCopyTo(Span().Slice(startpos)) ? startpos + bytes.Length : -startpos;
		}

		#endregion

		#region Primitive Reads

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref byte v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = Span()[startpos];

			return startpos + 1;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref bool v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = BitConverter.ToBoolean(Span().Slice(startpos, 1));

			return startpos + 1;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref char v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = BitConverter.ToChar(Span().Slice(startpos, 2));

			return startpos + 2;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref short v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = BitConverter.ToInt16(Span().Slice(startpos, 2));

			return startpos + 2;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref int v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = BitConverter.ToInt32(Span().Slice(startpos, 4));

			return startpos + 4;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref uint v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = BitConverter.ToUInt32(Span().Slice(startpos, 4));

			return startpos + 4;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref long v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = BitConverter.ToInt64(Span().Slice(startpos, 8));

			return startpos + 8;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref ulong v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = BitConverter.ToUInt64(Span().Slice(startpos, 8));

			return startpos + 8;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref double v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = BitConverter.ToDouble(Span().Slice(startpos, 8));

			return startpos + 8;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref Guid v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = new Guid(Span().Slice(startpos, 16));

			return startpos + 16;
		}

		/// <summary>
		/// Reads the value starting at startpos.
		/// </summary>
		/// <param name="v">The ref value to be updated.</param>
		/// <param name="startpos">Index in the fragment window.</param>
		/// <returns>The updated position as startpos + the value length.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If startpos is out of range</exception>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks and the lane is closed or cycled.
		/// Codes: AttemptToAccessWrongLaneCycle, AttemptToAccessClosedLane AttemptToAccessDisposedLane</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read(ref DateTime v, int startpos)
		{
			if (startpos < 0 || startpos >= Length)
				throw new ArgumentOutOfRangeException("startpos");

			laneCheck();
			v = DateTime.FromBinary(BitConverter.ToInt64(Span().Slice(startpos, 8)));

			return startpos + 8;
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

		public Span<T> ToSpan<T>() where T : struct => MemoryMarshal.Cast<byte, T>(Span());

		public bool IsDisposed => isDisposed;
		public int LaneCycle => laneCycle;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void laneCheck()
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

		/// <summary>
		/// Gets the fragment Span. 
		/// </summary>
		/// <param name="f">The fragment.</param>
		/// <exception cref="System.MemoryLaneException">If UseAccessChecks is on: 
		/// AttemptToAccessWrongLaneCycle, AttemptToAccessDisposedLane, AttemptToAccessClosedLane
		/// </exception>
		public static implicit operator Span<byte>(MemoryFragment f)
		{
			f.laneCheck();
			return f.Span();
		}

		public MemoryLane Lane => lane;
		protected MemoryLane lane;
		protected readonly int laneCycle;
		protected bool isDisposed;
		protected readonly Action Destructor;
	}
}
