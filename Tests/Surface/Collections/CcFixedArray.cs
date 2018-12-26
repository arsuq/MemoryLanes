//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using TestRunner;

//namespace Tests.Surface.Collections
//{
//	public class CCFixedArray : ITestSurface
//	{
//		public string Info => "Tests the ConcurrentArray class.";

//		public string FailureMessage { get; private set; }
//		public bool? Passed { get; private set; }
//		public bool IsComplete { get; private set; }
//		public bool RequiresArgs => false;

//		public async Task Run(IDictionary<string, List<string>> args)
//		{
//			try
//			{
//				var rdm = new Random();
//				var ccfa = new ConcurrentFixedArray<object>(200);

//				Parallel.For(0, 100, (i) =>
//				{
//					Thread.Sleep(rdm.Next(20, 100));
//					ccfa.Append(i);
//				});

//				var L = new List<object>(ccfa.Items());
//				L.Sort();

//				for (int i = 0; i < 100; i++)
//				{
//					if ((int)L[i] != i)
//					{
//						FailureMessage = "Parallel Append() fails.";
//						Passed = false;
//						break;
//					}
//					if (ccfa.IndexOf(L[i]) != i)
//					{
//						FailureMessage = "IndexOf() fails.";
//						Passed = false;
//						break;
//					}
//				}

//				"OK: Parallel Append()".AsTestSuccess();

//				ccfa.Reset(ccfa.Capacity);

//				if (ccfa.Count != 0 || ccfa.AppendPos != -1)
//				{
//					Passed = false;
//					FailureMessage = $"Reset is incorrect.";
//					return;
//				}

//				"OK: Reset".AsTestSuccess();

//				Parallel.For(0, 100, (i) =>
//				{
//					var p = ccfa.Append(i);
//					//$"+ i:{i} @{p}".Trace(ConsoleColor.Green);
//					Thread.Sleep(rdm.Next(20, 100));
//					ccfa.RemoveLast(out int x);
//					//$"- i:{i} @:{x}".Trace(ConsoleColor.Red);
//				});

//				var notNull = 0;
//				for (int i = 0; i < 100; i++)
//					if (ccfa[i] != null) notNull++;

//				if (ccfa.Count != 0 || ccfa.Count != notNull)
//				{
//					Passed = false;
//					FailureMessage = $"The Count is incorrect. Should be 0, it's {ccfa.Count}";
//					return;
//				}

//				if (ccfa.AppendPos != -1)
//				{
//					Passed = false;
//					FailureMessage = $"The AppendPos is incorrect. Should be -1, it's {ccfa.AppendPos}";
//					return;
//				}

//				"OK: Parallel Append and RemoveLast()".AsTestSuccess();

//				Passed = true;
//				IsComplete = true;
//			}
//			catch (Exception ex)
//			{
//				Passed = false;
//				FailureMessage = ex.Message;
//			}
//		}
//	}
//}
