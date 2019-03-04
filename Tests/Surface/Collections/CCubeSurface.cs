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
				var qb = new CCube<object>();

				void append_latency()
				{
					var CAP = int.MaxValue / 32;

					// Weirdly, preallocating CAP slots seems to make less than 300ms difference.
					var cube = new CCube<object>();

					// Before testing the setter with preallocated CAP, move the AppendIndex to CAP
					// cube.MoveAppendIndex(CAP, true);

					var start = DateTime.Now;

					// Parallel.For(0, CAP, (i) => cube.Append(i));

					for (int i = 0; i < CAP; i++)
						cube.Append(i);

					var cubeTime = DateTime.Now.Subtract(start);

					cube = null;
					GC.Collect(2);

					// Something expandable and concurrent.
					// The CCQ seems to be more efficient than the CCStack.
					var queue = new ConcurrentQueue<object>();
					start = DateTime.Now;

					// Parallel.For(0, CAP, (i) => stack.Push(i));

					//for (int i = 0; i < CAP; i++)
					//	queue.Enqueue(i);

					var qTime = DateTime.Now.Subtract(start);

					$"Allocating {CAP} objects: cube {cubeTime.TotalMilliseconds}ms stack {qTime.TotalMilliseconds} ".AsTestInfo();
				}

				//if (!parallelAppend(rdm, qb)) return;
				//if (!parallelAppendRemoveLast(rdm, qb)) return;
				//if (!shrink(qb)) return;
				//if (!expand(qb)) return;
				//if (!customExpand()) return;
				//if (!gears()) return;
				//if (!format()) return;
				//if (!take()) return;

				append_latency();

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
			var ccaexp = new CCube<object>((in int slots) => slots < 1500 ? slots * 2 : Convert.ToInt32(slots * 1.5));
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
			arr.ShiftGear(CCubeGear.P);
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
			arr.ShiftGear(CCubeGear.P);
			arr.Resize(0);
			arr.ShiftGear(CCubeGear.Straight);

			for (int i = 0; i < 200; i++)
			{
				arr.ShiftGear(CCubeGear.Straight);
				arr.Append(i);
				arr.ShiftGear(CCubeGear.Reverse);
				arr.RemoveLast(out int x);
			}

			int count = 0;

			Parallel.For(0, 200, (i) =>
			{
				Thread.Sleep(rdm.Next(20, 100));
				if (i % 2 == 0)
				{
					arr.ShiftGear(CCubeGear.Straight);
					arr.Append(i);
					Interlocked.Increment(ref count);
				}
				else
				{
					if (arr.ItemsCount > 1)
					{
						arr.ShiftGear(CCubeGear.Reverse);
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

				arr.ShiftGear(CCubeGear.N);

				Parallel.For(0, 200, (i) =>
				{
					var rid = rdm.Next(0, 40);
					arr[rid] = (int)arr[rid] + 1000;
				});

				"Gears.N".AsSuccess();

				arr.ShiftGear(CCubeGear.Reverse);

				Parallel.For(0, 200, (i) =>
				{
					arr.RemoveLast(out int pos);
				});

				"Gears.Reverse".AsSuccess();

				arr.ShiftGear(CCubeGear.P);
				arr.Resize(0);
				arr.OnGearShiftReset();

				Parallel.For(0, 200, (i) =>
				{
					arr.ShiftGear(CCubeGear.Straight, () => arr.Append(i));
					arr.ShiftGear(CCubeGear.Reverse, () => arr.RemoveLast(out int p));
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

			arr.ShiftGear(CCubeGear.N);
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
			var CAP = 50_000;
			int next = 0, sum = 0;
			var compareSum = (CAP / 2) * (1 + CAP);

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

			var als = 1024 + (2 * CCube<object>.DEF_EXP);

			if (cca.AllocatedSlots != als)
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
