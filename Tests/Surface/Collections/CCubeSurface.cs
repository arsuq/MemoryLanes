/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface.Collections
{
	public class CCubeSurface : ITestSurface
	{
		public string Info => "Tests the CCube class.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IsComplete { get; private set; }
		public bool IndependentLaunchOnly => false;

		public async Task Run(IDictionary<string, List<string>> args)
		{
			try
			{
				var rdm = new Random();
				var arr = new CCube<object>();

				void ccpos()
				{
					var p1 = new CCubePos(1292);
					var p2 = new CCubePos(132);
					var p3 = new CCubePos(4567);
					var p4 = new CCubePos(102345);
					var p5 = new CCubePos(193202345);
				}

				ccpos();

				if (!parallelAppend(rdm, arr)) return;
				if (!parallelAppendRemoveLast(rdm, arr)) return;
				if (!shrink(arr)) return;
				if (!expand(arr)) return;
				if (!customExpand()) return;
				if (!gears()) return;
				if (!format()) return;
				if (!take()) return;

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
			var ccaexp = new CCube<object>((slots, block, cap) =>
				slots < 1500 ? slots * 2 : Convert.ToInt32(slots * 1.5));

			var firstExp = ccaexp.BlockLength + 1;
			var secondExp = ccaexp.BlockLength * 2 + 1;

			for (int i = 0; i < 5000; i++)
			{
				ccaexp.Append(i);

				if (i == firstExp)
				{
					if (ccaexp.AllocatedSlots != ccaexp.BlockLength * 2)
					{
						Passed = false;
						FailureMessage = $"Expansion is wrong, Expected 20, got {ccaexp.AllocatedSlots}";
						return false;
					}
				}

				if (i == secondExp)
				{
					if (ccaexp.AllocatedSlots != ccaexp.BlockLength * 3)
					{
						Passed = false;
						FailureMessage = $"Expansion is wrong, Expected 30, got {ccaexp.AllocatedSlots}";
						return false;
					}
					else break;
				}
			}

			"Custom growth".AsSuccess();

			return true;
		}

		bool expand(CCube<object> arr)
		{
			var doubleCap = arr.AllocatedSlots * 2;
			arr.Resize(doubleCap);

			if (arr.AllocatedSlots != doubleCap)
			{
				Passed = false;
				FailureMessage = $"The Capacity is wrong. Expected {doubleCap} got {arr.AllocatedSlots}";
				return false;
			}

			"Resize() expanding".AsSuccess();

			return true;
		}

		bool shrink(CCube<object> arr)
		{
			arr.ShiftGear(CCArrayGear.P);
			arr.Resize(20000);

			var oldCap = arr.AllocatedSlots;
			var newCap = oldCap / 3;

			arr.Resize(newCap);

			var p = new CCubePos(newCap);

			var slots = p.D0 * arr.BlockLength * arr.BlockLength + p.D1 * arr.BlockLength;

			if (arr.AllocatedSlots != slots)
			{
				Passed = false;
				FailureMessage = $"The Capacity is wrong. Expected {slots} got {arr.AllocatedSlots}";
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

		bool parallelAppendRemoveLast(Random rdm, CCube<object> arr)
		{
			arr.ShiftGear(CCArrayGear.P);
			arr.Resize(0);
			arr.ShiftGear(CCArrayGear.Straight);

			for (int i = 0; i < 200; i++)
			{
				arr.ShiftGear(CCArrayGear.Straight);
				arr.Append(i);
				arr.ShiftGear(CCArrayGear.Reverse);
				arr.RemoveLast(out int x);
			}

			int count = 0;

			Parallel.For(0, 200, (i) =>
			{
				Thread.Sleep(rdm.Next(20, 100));
				if (i % 2 == 0)
				{
					arr.ShiftGear(CCArrayGear.Straight);
					arr.Append(i);
					Interlocked.Increment(ref count);
				}
				else
				{
					if (arr.ItemsCount > 1)
					{
						arr.ShiftGear(CCArrayGear.Reverse);
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

		bool parallelAppend(Random rdm, CCube<object> arr)
		{
			var controlArr = new List<object>();

			Parallel.For(0, 2000, (i) =>
			{
				Thread.Sleep(rdm.Next(2, 60));
				arr.Append((object)i);
			});

			for (int i = 0; i <= arr.AppendIndex; i++)
				controlArr.Add(arr[i]);

			var L = new List<object>(arr.NotNullItems());
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
			var arr = new CCube<object>();
			var rdm = new Random();

			try
			{
				arr.OnGearShift += (g) => $"On gear shift {g}".AsInnerInfo();

				Parallel.For(0, 200, (i) =>
				{
					var pos = arr.Append(i);
					var x = (int)arr[pos];
					arr[pos] = x + 1000;
				});

				"Gears.Straight".AsSuccess();

				arr.ShiftGear(CCArrayGear.N);

				Parallel.For(0, 200, (i) =>
				{
					var rid = rdm.Next(0, 40);
					arr[rid] = (int)arr[rid] + 1000;
				});

				"Gears.N".AsSuccess();

				arr.ShiftGear(CCArrayGear.Reverse);

				Parallel.For(0, 200, (i) =>
				{
					arr.RemoveLast(out int pos);
				});

				"Gears.Reverse".AsSuccess();

				arr.ShiftGear(CCArrayGear.P);
				arr.Resize(0);
				arr.OnGearShiftReset();

				Parallel.For(0, 200, (i) =>
				{
					arr.ShiftGear(CCArrayGear.Straight, () => arr.Append(i));
					arr.ShiftGear(CCArrayGear.Reverse, () => arr.RemoveLast(out int p));
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
			var arr = new CCube<object>();

			for (int i = 0; i < 100; i++)
				arr.Append(i);

			arr.ShiftGear(CCArrayGear.N);
			object o = 3;
			arr.Format(o);

			foreach (var item in arr.NotNullItems())
				if (item != o)
				{
					Passed = false;
					FailureMessage = "Format fails";
					return false;
				}

			"Fotmat()".AsSuccess();

			return true;
		}

		bool take()
		{
			var cca = new CCube<object>();
			var CAP = cca.Capacity;
			int next = 0, sum = 0;
			int compareSum = (CAP / 2) * (1 + CAP);

			// Producers
			var P = new Task[10];

			for (int i = 0; i < P.Length; i++)
			{
				P[i] = new Task(() =>
				{
					for (int j = 0; j < CAP; j++)
					{
						try
						{
							if (cca.AppendIndex >= CAP) break;
							var nextInt = Interlocked.Increment(ref next);
							if (nextInt <= CAP) cca.Append(nextInt);
							else break;
						}
						catch
						{
							break;
						}
					}
				});
			}

			// Consumers
			var C = new Task[10];

			for (int i = 0; i < P.Length; i++)
			{
				C[i] = new Task(() =>
				{
					for (int j = 0; j < CAP; j++)
					{
						try
						{
							while (j > cca.AppendIndex)
								Thread.Sleep(20);

							var o = cca.Take(j);

							if (o != null)
							{
								int v = (int)o;
								Interlocked.Add(ref sum, v);
							}
						}
						catch (InvariantException)
						{
							break;
						}
					}

				});
			}

			for (int i = 0; i < 10; i++)
			{
				P[i].Start();
				C[i].Start();
			}

			Task.WaitAll(P);
			Task.WaitAll(C);

			if (cca.AllocatedSlots != CAP)
			{
				FailureMessage = $"Wrong AllocatedSlots {cca.AllocatedSlots}, expected {CAP}";
				Passed = false;
				return false;
			}

			for (int i = 0; i < CAP; i++)
				if (cca[i] != null)
				{
					FailureMessage = $"Take at {i} failed. There is a value: {cca[i]}";
					Passed = false;
					return false;
				}

			if (sum != compareSum)
			{
				FailureMessage = "Take is wrong.";
				Passed = false;
				return false;
			}

			"Concurrent Append() and Take()".AsSuccess();

			return true;
		}
	}
}
