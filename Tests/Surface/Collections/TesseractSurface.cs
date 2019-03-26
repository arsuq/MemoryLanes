/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

#define TSR_INT

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface.Collections
{

#if TSR_INT
	using OBJ = Int32;
	using ARG = TesseractCell<int>;
	using TSR = Tesseract<TesseractCell<int>>;
#else
	using TSR = Tesseract<object>;
	using OBJ = Object;
	using ARG = Object;
#endif

	public class TesseractSurface : ITestSurface
	{
		public string Info => "Tests either the Tesseract<T> class.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IsComplete { get; private set; }
		public bool IndependentLaunchOnly => false;

#if TSR_INT
		public const int NULL = 0;
#else
		public const object NULL = null;
#endif

		public async Task Run(IDictionary<string, List<string>> args)
		{
			try
			{
				var rdm = new Random();
				var qb = new TSR();

				pos();

				if (!parallelAppend(rdm, qb)) return;
				if (!parallelAppendRemoveLast(rdm, qb)) return;
				if (!shrink(qb)) return;
				if (!expand(qb)) return;
				if (!customExpand()) return;
				if (!gears()) return;
				if (!format()) return;
				if (!take()) return;

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

		bool pos()
		{
			var I = new int[] { 111, 333, 517, 67440, 8_888_888, 1_234_432_323 };
			var E = new (int d0, int d1, int d2, int d3)[] {
				(0, 0, 0, 111),
				(0, 0, 1, 77),
				(0, 0, 2, 5),
				(0, 1, 7, 112),
				(0, 135, 162, 56),
				(73, 147, 241, 67),
			};

			for (int i = 0; i < I.Length; i++)
			{
				var p = new TesseractPos(I[i]);
				var c = E[i];

				if (p.D0 != c.d0 || p.D1 != c.d1 || p.D2 != c.d2 || p.D3 != c.d3)
				{
					FailureMessage = "TesseractPos ctor is incorrect.";
					Passed = false;
					return false;
				}
			}

			return true;
		}

		void append_latency()
		{
			var CAP = int.MaxValue / 32; // 67108863
			long startTicks = 0;
			long stopTicks = 0;
			TimeSpan cubeTime;
			TimeSpan queueTime;

			// Preallocating CAP slots produces weird results, try it...
			var tsr = new TSR(0, false);

			// Before testing the setter with preallocated CAP, move the AppendIndex to CAP
			// cube.MoveAppendIndex(CAP, true);

			// Just to be sure that the positioning will remain
			Interlocked.Exchange(ref startTicks, DateTime.Now.Ticks);

			Parallel.For(0, CAP, new ParallelOptions() { MaxDegreeOfParallelism = 100 }, (i) => tsr.Append(i));

			//for (int i = 0; i < CAP; i++)
			//	tsr.Append(i);

			Interlocked.Exchange(ref stopTicks, DateTime.Now.Ticks);
			cubeTime = new TimeSpan(stopTicks - startTicks);
			tsr = null;
			GC.Collect(2);

			// Something expandable and concurrent.
			// The ccQ seems to be more efficient than the ccStack.
			var queue = new ConcurrentQueue<OBJ>();

			Interlocked.Exchange(ref startTicks, DateTime.Now.Ticks);

			Parallel.For(0, CAP, new ParallelOptions() { MaxDegreeOfParallelism = 100 }, (i) => queue.Enqueue(i));

			// for (int i = 0; i < CAP; i++)
			//	queue.Enqueue(i);

			Interlocked.Exchange(ref stopTicks, DateTime.Now.Ticks);
			queueTime = new TimeSpan(stopTicks - startTicks);

			$"Allocating {CAP} objects: Tesseract {cubeTime.TotalMilliseconds}ms Queue {queueTime.TotalMilliseconds}ms ".AsTestInfo();
		}

		bool customExpand()
		{
			var ccaexp = new TSR((in int slots) => slots < 300 ? slots * 2 : Convert.ToInt32(slots * 1.5));
			var firstExp = ccaexp.Side + 1;
			var secondExp = ccaexp.Side * 2 + 1;

			for (int i = 0; i < 800; i++)
			{
				ccaexp.Append(i);

				if (i == firstExp)
				{
					if (ccaexp.AllocatedSlots != ccaexp.Side * 2)
					{
						Passed = false;
						FailureMessage = $"Expansion is wrong, Expected 20, got {ccaexp.AllocatedSlots}";
						return false;
					}
				}

				if (i == secondExp)
				{
					if (ccaexp.AllocatedSlots != ccaexp.Side * 3)
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

		bool expand(TSR arr)
		{
			var doubleCap = arr.AllocatedSlots * 2;
			arr.Resize(doubleCap, true);

			if (arr.AllocatedSlots != doubleCap)
			{
				Passed = false;
				FailureMessage = $"The Capacity is wrong. Expected {doubleCap} got {arr.AllocatedSlots}";
				return false;
			}

			"Resize() expanding".AsSuccess();

			return true;
		}

		bool shrink(TSR arr)
		{
			arr.Clutch(TesseractGear.P);
			arr.Resize(0, false);
			arr.Resize(20000, true);
			arr.MoveAppendIndex(arr.AllocatedSlots - 1);

			var oldCap = arr.AllocatedSlots;
			var newCap = oldCap / 3;
			var side = arr.Side;

			arr.Resize(newCap, false);

			var p = new TesseractPos(newCap);

			var slots = p.D2 * side + side;

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

		bool parallelAppendRemoveLast(Random rdm, TSR arr)
		{
			arr.Clutch(TesseractGear.P);
			arr.Resize(0, false);
			arr.Clutch(TesseractGear.Straight);
			var pos = 0;

			for (int i = 0; i < 200; i++)
			{
				arr.Clutch(TesseractGear.Straight);
				arr.Append(i);
				arr.Clutch(TesseractGear.Reverse);
				arr.RemoveLast(ref pos);
			}

			int count = 0;

			Parallel.For(1, 201, (i) =>
			{
				Thread.Sleep(rdm.Next(20, 100));
				if (i % 2 == 0)
				{
					arr.Clutch(TesseractGear.Straight);
					arr.Append(i);
					Interlocked.Increment(ref count);
				}
				else
				{
					if (arr.ItemsCount > 1)
					{
						arr.Clutch(TesseractGear.Reverse);
						arr.RemoveLast(ref pos);
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

		bool parallelAppend(Random rdm, TSR arr)
		{
			var controlArr = new List<OBJ>();

			// Do not add 0 for the TSR_INT will not count it 
			Parallel.For(1, 2000, (i) =>
			{
				Thread.Sleep(rdm.Next(2, 60));
				arr.Append(i);
			});

			for (int i = 0; i <= arr.AppendIndex; i++)
				controlArr.Add(arr[i]);

			var L = new List<ARG>(arr.NotNullItems());
			L.Sort();

			for (int i = 0; i < 100; i++)
			{
				if ((int)L[i] != i + 1)
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
			var arr = new TSR();
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

				arr.Clutch(TesseractGear.N);

				Parallel.For(0, 200, (i) =>
				{
					var rid = rdm.Next(0, 40);
					arr[rid] = (int)arr[rid] + 1000;
				});

				"Gears.N".AsSuccess();

				arr.Clutch(TesseractGear.Reverse);
				var _ = 0;

				Parallel.For(0, 200, (i) =>
				{
					arr.RemoveLast(ref _);
				});

				"Gears.Reverse".AsSuccess();

				arr.Clutch(TesseractGear.P);
				arr.Resize(0, false);
				arr.OnGearShiftReset();

				Parallel.For(0, 200, (i) =>
				{
					arr.Clutch(TesseractGear.Straight, () => arr.Append(i));
					arr.Clutch(TesseractGear.Reverse, () => arr.RemoveLast(ref _));
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
			var arr = new TSR();

			for (int i = 0; i < 100; i++)
				arr.Append(i);

			arr.Clutch(TesseractGear.N);
			OBJ o = 3;
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
			var tsr = new TSR();
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
							var nextInt = Interlocked.Increment(ref next);
							if (nextInt <= CAP)
							{
								var appIdx = tsr.Append(nextInt);
								if (appIdx < 0) $"Appended {nextInt} at -1".AsError();
							}
							else break;
						}
						catch
						{
							"Breaking Producer".AsInnerInfo();
							break;
						}
					}
				});
			}

			// Consumers
			var C = new Task[10];

			for (int i = 0; i < C.Length; i++)
			{
				C[i] = new Task(() =>
				{
					for (int j = 0; j < CAP; j++)
					{
						try
						{
							var waits = 0;

							while (j > tsr.AppendIndex)
							{
								Thread.Sleep(1);
								waits++;

								if (waits > 10_000)
								{
									$"j: {j} AppendIndex: {tsr.AppendIndex} ".AsError();
									waits = 0;
								}
							}

							var o = tsr.Take(j);

							if (o != NULL)
							{
								int v = (int)o;
								Interlocked.Add(ref sum, v);
							}
						}
						catch (InvariantException)
						{
							"Consumer Invariant exception".AsError();
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
			"Producers are Done".AsInfo();
			Task.WaitAll(C);

			var allocs = CAP / TSR.DEF_EXP;
			if (CAP % TSR.DEF_EXP != 0) allocs++;

			var als = TSR.SIDE + (allocs * TSR.DEF_EXP);

			if (tsr.AllocatedSlots != als)
			{
				FailureMessage = $"Wrong AllocatedSlots {tsr.AllocatedSlots}, expected {CAP}";
				Passed = false;
				return false;
			}

			for (int i = 0; i < CAP; i++)
				if (tsr[i] != NULL)
				{
					FailureMessage = $"Take at {i} failed. There is a value: {tsr[i]}";
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
