using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface.Collections
{
	public class CCArray : ITestSurface
	{
		public string Info => "Tests the ConcurrentArray class.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IsComplete { get; private set; }
		public bool RequiresArgs => false;

		public async Task Run(IDictionary<string, List<string>> args)
		{
			try
			{
				var rdm = new Random();
				var arr = new ConcurrentArray<object>(10, 40);

				if (!parallelAppend(rdm, arr)) return;
				if (!parallelAppendRemoveLast(rdm, arr)) return;
				if (!shrink(arr)) return;
				if (!expand(arr)) return;
				if (!customExpand()) return;
				if (!gears()) return;
				if (!format()) return;

				Passed = true;
				IsComplete = true;
			}
			catch (Exception ex)
			{
				Passed = false;
				FailureMessage = ex.Message;
			}
		}

		bool customExpand()
		{
			var ccaexp = new ConcurrentArray<object>(10, 100, 1,
				(ca) => ca.Capacity < 20 ? ca.Capacity * 2 : Convert.ToInt32(ca.Capacity * 1.5));

			for (int i = 0; i < 50; i++)
			{
				ccaexp.Append(i);

				if (i == 10)
				{
					if (ccaexp.Capacity != 20)
					{
						Passed = false;
						FailureMessage = $"Expansion is wrong, Expected 20, got {ccaexp.Capacity}";
						return false;
					}
				}

				if (i == 20)
				{
					if (ccaexp.Capacity != 30)
					{
						Passed = false;
						FailureMessage = $"Expansion is wrong, Expected 30, got {ccaexp.Capacity}";
						return false;
					}
				}
			}

			"Custom growth".AsSuccess();

			return true;
		}

		bool expand(ConcurrentArray<object> arr)
		{
			var doubleCap = arr.Capacity * 2;
			arr.Resize(doubleCap);

			if (arr.Capacity != doubleCap)
			{
				Passed = false;
				FailureMessage = $"The Capacity is wrong. Expected {doubleCap} got {arr.Capacity}";
				return false;
			}

			"Resize() expanding".AsSuccess();

			return true;
		}

		bool shrink(ConcurrentArray<object> arr)
		{
			var oldCap = arr.Capacity;
			var newCap = oldCap / 3;

			arr.ShiftGear(ConcurrentArray<object>.Gear.P);
			arr.Resize(newCap);

			var newCapTilesCount = (newCap / arr.BlockLength);
			if (newCap % arr.BlockLength != 0) newCapTilesCount++;
			var newCapTiled = newCapTilesCount * arr.BlockLength;

			if (arr.Capacity != newCapTiled)
			{
				Passed = false;
				FailureMessage = $"The Capacity is wrong. Expected {newCapTiled} got {arr.Capacity}";
				return false;
			}

			if (arr.AppendIndex != newCap - 1)
			{
				Passed = false;
				FailureMessage = $"The AppendIndex should be {newCap - 1}, it's {arr.AppendIndex}";
				return false;
			}

			"Resize() shrinking".AsSuccess();

			return true;
		}

		bool parallelAppendRemoveLast(Random rdm, ConcurrentArray<object> arr)
		{
			arr.ShiftGear(ConcurrentArray<object>.Gear.P);
			arr.Resize(0);
			arr.ShiftGear(ConcurrentArray<object>.Gear.Straight);

			for (int i = 0; i < 200; i++)
			{
				arr.ShiftGear(ConcurrentArray<object>.Gear.Straight);
				arr.Append(i);
				arr.ShiftGear(ConcurrentArray<object>.Gear.Reverse);
				arr.RemoveLast(out int x);
			}

			int count = 0;

			Parallel.For(0, 200, (i) =>
			{
				Thread.Sleep(rdm.Next(20, 100));
				if (i % 2 == 0)
				{
					arr.ShiftGear(ConcurrentArray<object>.Gear.Straight);
					arr.Append(i);
					Interlocked.Increment(ref count);
				}
				else
				{
					if (arr.ItemsCount > 1)
					{
						arr.ShiftGear(ConcurrentArray<object>.Gear.Reverse);
						arr.RemoveLast(out int x);
						Interlocked.Decrement(ref count);
					}
				}
			});

			if (arr.ItemsCount != count)
			{
				Passed = false;
				FailureMessage = $"The ItemsCount is incorrect. Should be {count}, it's {arr.ItemsCount}";
				return false;
			}

			if (arr.AppendIndex != count - 1)
			{
				Passed = false;
				FailureMessage = $"The AppendIndex is incorrect. Should be {count - 1} for 100 items, it's {arr.AppendIndex}";
				return false;
			}

			"Parallel Append and RemoveLast()".AsSuccess();

			return true;
		}

		bool parallelAppend(Random rdm, ConcurrentArray<object> arr)
		{
			var controlArr = new List<object>();

			Parallel.For(0, 200, (i) =>
			{
				Thread.Sleep(rdm.Next(20, 100));
				arr.Append((object)i);
			});

			for (int i = 0; i <= arr.AppendIndex; i++)
				controlArr.Add(arr[i]);

			var L = new List<object>(arr.Items());
			L.Sort();

			for (int i = 0; i < 100; i++)
			{
				if ((int)L[i] != i)
				{
					FailureMessage = "Parallel Append() fails.";
					Passed = false;
					return false;
				}

				var o = controlArr[i];

				if (arr.IndexOf(o) != controlArr.IndexOf(o))
				{
					FailureMessage = "IndexOf() fails.";
					Passed = false;
					return false;
				}
			}

			"Parallel Append()".AsSuccess();

			return true;
		}

		bool gears()
		{
			var arr = new ConcurrentArray<object>(10, 100);
			var rdm = new Random();

			try
			{
				Parallel.For(0, 200, (i) =>
				{
					var pos = arr.Append(i);
					var x = (int)arr[pos];
					arr[pos] = x + 1000;
				});

				"Gears.Straight".AsSuccess();

				arr.ShiftGear(ConcurrentArray<object>.Gear.N);

				Parallel.For(0, 200, (i) =>
				{
					var rid = rdm.Next(0, 40);
					arr[rid] = (int)arr[rid] + 1000;
				});

				"Gears.N".AsSuccess();

				arr.ShiftGear(ConcurrentArray<object>.Gear.Reverse);

				Parallel.For(0, 200, (i) =>
				{
					arr.RemoveLast(out int pos);
				});

				"Gears.Reverse".AsSuccess();

				arr.ShiftGear(ConcurrentArray<object>.Gear.P);
				arr.Resize(0);

				Parallel.For(0, 200, (i) =>
				{
					arr.ShiftGear(ConcurrentArray<object>.Gear.Straight, () => arr.Append(i));
					arr.ShiftGear(ConcurrentArray<object>.Gear.Reverse, () => arr.RemoveLast(out int p));
				});

				"Competing shifts".AsSuccess();
			}
			catch (Exception ex)
			{
				FailureMessage = "Gears fails";
				Passed = false;
				return false;
			}

			return true;
		}

		bool format()
		{
			var arr = new ConcurrentArray<object>(10, 10);

			for (int i = 0; i < 100; i++)
				arr.Append(i);

			object o = 3;
			arr.Format(o);

			foreach (var item in arr.Items())
				if (item != o)
				{
					Passed = false;
					FailureMessage = "Format fails";
					return false;
				}

			return true;
		}

	}
}
