//using System.Collections.Generic;
//using System.Threading;

//namespace System.Collections.Concurrent
//{
//	/// <summary>
//	/// Provides a simple thread safe API for accessing a fixed length array.
//	/// All methods use atomic operations, only Append and RemoveLast are 
//	/// spin lock protected.
//	/// </summary>
//	/// <typeparam name="T">Must be a class</typeparam>
//	public class ConcurrentFixedArray<T> where T : class
//	{
//		/// <summary>
//		/// Fixes the length.
//		/// </summary>
//		/// <param name="capacity">The total length.</param>
//		public ConcurrentFixedArray(int capacity) => array = new T[capacity];

//		/// <summary>
//		/// The last Append position. It can only increase up to the capacity.
//		/// </summary>
//		public int AppendPos => Volatile.Read(ref appendPos);

//		/// <summary>
//		/// Counts the non null items in the array.
//		/// </summary>
//		public int Count => Volatile.Read(ref count);

//		/// <summary>
//		/// The array length.
//		/// </summary>
//		public int Capacity => array.Length;

//		/// <summary>
//		/// Access to the individual cells.
//		/// </summary>
//		/// <param name="index">The position in the array, 
//		/// must be less than the AppendPos for setting and less than Capacity for getting.</param>
//		/// <returns>The object reference at the index</returns>
//		/// <exception cref="ArgumentOutOfRangeException">If the index is negative or greater than the AppendPos.</exception>
//		public T this[int index]
//		{
//			get
//			{
//				if (index < 0 || index > array.Length)
//					throw new ArgumentOutOfRangeException("index");

//				return Volatile.Read(ref array[index]);
//			}
//			set
//			{
//				if (index < 0 || index > AppendPos)
//					throw new ArgumentOutOfRangeException("index");

//				var original = Interlocked.Exchange<T>(ref array[index], value);
//				if (value != null) Interlocked.Increment(ref count);
//				else if (original != null) Interlocked.Decrement(ref count);
//			}
//		}

//		/// <summary>
//		/// Appends an item after the AppendPos index.
//		/// </summary>
//		/// <param name="item">The object reference</param>
//		/// <returns>The index of the item</returns>
//		/// <exception cref="System.SynchronizationException">If fails to take the lock.</exception>
//		public int Append(T item)
//		{
//			var pos = -1;

//			if (item != null)
//			{
//				bool isLocked = false;
//				spin.Enter(ref isLocked);

//				if (isLocked)
//				{
//					pos = Interlocked.Increment(ref appendPos);
//					Interlocked.Exchange<T>(ref array[pos], item);
//					Interlocked.Increment(ref count);

//					spin.Exit();
//				}
//				else throw new SynchronizationException(SynchronizationException.Code.LockAcquisition);
//			}

//			return pos;
//		}

//		/// <summary>
//		/// Decrements the AppendPos, nulls the corresponding cell and returns the item that was there.
//		/// Note that since there are no locks here, the array cell could change several times from the
//		/// moment of getting the index up to the actual return.
//		/// </summary>
//		/// <param name="pos">The item position.</param>
//		/// <returns>The removed item.</returns>
//		/// <exception cref="System.SynchronizationException">If fails to take the lock.</exception>
//		/// <exception cref="System.InvalidOperationException">If there are no items.</exception>
//		public T RemoveLast(out int pos)
//		{
//			bool isLocked = false;
//			spin.Enter(ref isLocked);

//			if (isLocked)
//			{
//				pos = AppendPos;

//				if (pos >= 0)
//				{
//					Interlocked.Decrement(ref count);
//					var removed = Interlocked.Exchange<T>(ref array[pos], null);
//					Interlocked.Decrement(ref appendPos);
//					spin.Exit();

//					return removed;
//				}
//				else
//				{
//					spin.Exit();

//					throw new InvalidOperationException("Nothing to remove");
//				}
//			}
//			else throw new SynchronizationException(SynchronizationException.Code.LockAcquisition);
//		}

//		/// <summary>
//		/// First searches for the item index and then nulls the array cell and decrements the Count.
//		/// </summary>
//		/// <remarks>
//		/// Because there is no lock protection, the same cell could be 
//		/// updated before the atomic null exchange by another thread,
//		/// thus overriding the new value as null.
//		/// </remarks>
//		/// <param name="item">The object reference</param>
//		/// <returns>True if found and null-ed</returns>
//		public bool Remove(T item)
//		{
//			if (item == null) return false;

//			var idx = IndexOf(item);

//			if (idx >= 0)
//			{
//				Interlocked.Exchange<T>(ref array[idx], null);
//				Interlocked.Decrement(ref count);

//				return true;
//			}

//			return false;
//		}

//		/// <summary>
//		/// Resets the AppendPos to -1 and the Count to 0.
//		/// </summary>
//		/// <param name="newsize">If greater than 0 allocates a new array.</param>
//		/// <exception cref="System.SynchronizationException">If fails to take the lock.</exception>
//		public void Reset(int newsize = 0)
//		{
//			bool isLocked = false;
//			spin.Enter(ref isLocked);

//			if (isLocked)
//			{
//				Interlocked.Exchange(ref appendPos, -1);
//				Interlocked.Exchange(ref count, 0);
//				if (newsize > 0) Interlocked.Exchange(ref array, new T[newsize]);

//				spin.Exit();
//			}
//			else throw new SynchronizationException(SynchronizationException.Code.LockAcquisition);
//		}

//		/// <summary>
//		/// Iterates all cells from 0 up to AppendPos (inclusive) and yields the 
//		/// item if it's not null at the time of the check.
//		/// </summary>
//		/// <returns>A not null item.</returns>
//		public IEnumerable<T> Items()
//		{
//			var LEN = AppendPos;
//			for (int i = 0; i <= LEN; i++)
//				if (Volatile.Read(ref array[i]) != null)
//					yield return array[i];
//		}

//		/// <summary>
//		/// Searches for an item by traversing all cells up to AppendPos, which is 
//		/// the furthest index in the array.
//		/// </summary>
//		/// <param name="item">The object ref</param>
//		/// <returns>A positive value if the item is found, -1 otherwise.</returns>
//		public int IndexOf(T item)
//		{
//			var LEN = AppendPos;
//			for (var i = 0; i <= LEN; i++)
//				if (Volatile.Read(ref array[i]) == item)
//					return i;

//			return -1;
//		}

//		SpinLock spin = new SpinLock();
//		T[] array = null;
//		int appendPos = -1;
//		int count;
//	}
//}
