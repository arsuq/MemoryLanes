using System.Collections.Generic;
using System.Threading;

namespace System.Collections.Concurrent
{
	/// <summary>
	/// A fixed length array with a simple API to get/set object references.
	/// </summary>
	/// <typeparam name="T">Must be a class</typeparam>
	public class ConcurrentFixedArray<T> where T : class
	{
		/// <summary>
		/// Fixes the length. It will never change.
		/// </summary>
		/// <param name="capacity">The total length.</param>
		public ConcurrentFixedArray(int capacity) => array = new T[capacity];

		/// <summary>
		/// The biggest Add or set offset. It can only increase up to the capacity.
		/// Using the indexer may create holes in the array as it shifts the appendPos
		/// and all subsequent appends will queue after that position.
		/// </summary>
		public int AppendPos => Volatile.Read(ref appendPos);

		/// <summary>
		/// Counts the non null items in the array.
		/// </summary>
		public int Count => Volatile.Read(ref count);

		/// <summary>
		/// The array length
		/// </summary>
		public int Capacity => array.Length;

		/// <summary>
		/// Access the individual cells.
		/// The setter updates the appendPos if the index is bigger which could lead to 
		/// lost append cells for the Append() only adds items after AppendPos.
		/// If the setter value is null and the updated cell is not null the Count will be decremented.
		/// If the setter value is not null the Counter is incremented.
		/// </summary>
		/// <remarks>
		/// Use either Append/RemoveLast or the indexer; mixed access could  
		/// </remarks>
		/// <param name="index">The position in the array</param>
		/// <returns>The object reference at the index</returns>
		/// <exception cref="ArgumentOutOfRangeException">index</exception>
		public T this[int index]
		{
			get => Volatile.Read(ref array[index]);
			set
			{
				if (index < 0 || index > array.Length)
					throw new ArgumentOutOfRangeException("index");

				if (index > AppendPos) Interlocked.Exchange(ref appendPos, index);
				var original = Interlocked.Exchange<T>(ref array[index], value);
				if (value != null) Interlocked.Increment(ref count);
				else if (original != null) Interlocked.Decrement(ref count);
			}
		}

		/// <summary>
		/// Appends an item after the AppendPos index.
		/// </summary>
		/// <param name="item">The object reference</param>
		/// <returns>The index of the item</returns>
		public int Append(T item)
		{
			var pos = -1;

			if (item != null)
			{
				pos = Interlocked.Increment(ref appendPos);
				this[pos] = item;
			}

			return pos;
		}

		/// <summary>
		/// Decrements the AppendPos, nulls the corresponding cell and returns the item that was there.
		/// Note that since there are no locks here, the array cell could change several times from the
		/// moment of getting the index up to the actual return.
		/// </summary>
		/// <param name="pos">The item position.</param>
		/// <returns>The removed item.</returns>
		public T RemoveLast(out int pos)
		{
			T theItem = null;
			pos = Interlocked.Decrement(ref appendPos);

			if (pos >= 0)
			{
				theItem = this[pos];
				this[pos] = null;
			}

			return theItem;
		}

		/// <summary>
		/// Looks for the item and nulls the array cell, 
		/// this will decrement the Count value.
		/// </summary>
		/// <param name="item">The object reference</param>
		/// <returns>True if found and null-ed</returns>
		public bool Remove(T item)
		{
			if (item == null) return false;

			var idx = IndexOf(item);

			if (idx >= 0)
			{
				this[idx] = null;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Iterates all cells from 0 up to AppendPos and yields the 
		/// item if it's not null at the time of the check.
		/// </summary>
		/// <returns>A not null item.</returns>
		public IEnumerable<T> Items()
		{
			T item = null;
			var LEN = AppendPos;
			for (int i = 0; i <= LEN; i++)
			{
				item = this[i];
				if (item != null)
					yield return item;
			}
		}

		/// <summary>
		/// Searches for an item by traversing all cells up to AppendPos, which is 
		/// the furthest index in the array.
		/// </summary>
		/// <param name="item">The object ref</param>
		/// <returns>A positive value if the item is found, -1 otherwise.</returns>
		public int IndexOf(T item)
		{
			for (var i = 0; i <= AppendPos; i++)
				if (this[i] == item)
					return i;

			return -1;
		}

		T[] array = null;
		int appendPos = -1;
		int count;
	}
}
