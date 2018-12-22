using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestRunner;

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
				var ccfas = new ConcurrentArray<object>(10);

				Parallel.For(0, 100, (i) =>
				{
					Thread.Sleep(rdm.Next(20, 100));
					ccfas.Append(i);
				});

				var L = new List<object>(ccfas.Items());
				L.Sort();

				for (int i = 0; i < 100; i++)
				{
					if ((int)L[i] != i)
					{
						FailureMessage = "Parallel Append() fails.";
						Passed = false;
						break;
					}
					if (ccfas.IndexOf(L[i]) != i)
					{
						FailureMessage = "IndexOf() fails.";
						Passed = false;
						break;
					}
				}

				"OK: Parallel Append()".AsTestSuccess();

				Parallel.For(0, 200, (i) =>
				{
					Thread.Sleep(rdm.Next(20, 100));
					ccfas.Append(i);
					ccfas.RemoveLast(out int x);
				});

				if (ccfas.Count != 100)
				{
					Passed = false;
					FailureMessage = $"The Count is incorrect. Should be 100, it's {ccfas.Count}";
					return;
				}

				if (ccfas.AppendPos != 99)
				{
					Passed = false;
					FailureMessage = $"The AppendPos is incorrect. Should be 99 for 100 items, it's {ccfas.AppendPos}";
					return;
				}

				"OK: Parallel Append and RemoveLast()".AsTestSuccess();

				var oldCap = ccfas.Capacity;
				var newCap = oldCap / 3;

				ccfas.Resize(newCap);

				var newCapTilesCount = (newCap / ccfas.BaseLength);
				if (newCap % ccfas.BaseLength != 0) newCapTilesCount++;
				var newCapTiled = newCapTilesCount * ccfas.BaseLength;

				if (ccfas.Capacity != newCapTiled)
				{
					Passed = false;
					FailureMessage = $"The Capacity is wrong. Expected {newCapTiled} got {ccfas.Capacity}";
					return;
				}

				if (ccfas.AppendPos != newCap - 1)
				{
					Passed = false;
					FailureMessage = $"The AppendPos should be {newCap - 1}, it's {ccfas.AppendPos}";
					return;
				}

				"OK: Resize() shrinking".AsTestSuccess();

				var doubleCap = ccfas.Capacity * 2;
				ccfas.Resize(doubleCap);

				if (ccfas.Capacity != doubleCap)
				{
					Passed = false;
					FailureMessage = $"The Capacity is wrong. Expected {doubleCap} got {ccfas.Capacity}";
					return;
				}

				"OK: Resize() expanding".AsTestSuccess();


				var ccaexp = new ConcurrentArray<object>(10, 1, (len) => len < 20 ? len * 2 : Convert.ToInt32(len * 1.5));

				for (int i = 0; i < 50; i++)
				{
					ccaexp.Append(i);

					if (i == 10)
					{
						if (ccaexp.Capacity != 20)
						{
							Passed = false;
							FailureMessage = $"Expansion is wrong, Expected 20, got {ccaexp.Capacity}";
							return;
						}
					}

					if (i == 20)
					{
						if (ccaexp.Capacity != 30)
						{
							Passed = false;
							FailureMessage = $"Expansion is wrong, Expected 30, got {ccaexp.Capacity}";
							return;
						}
					}
				}

				"OK: Custom growth".AsTestSuccess();

				Passed = true;
				IsComplete = true;
			}
			catch (Exception ex)
			{
				Passed = false;
				FailureMessage = ex.Message;
			}
		}
	}
}
