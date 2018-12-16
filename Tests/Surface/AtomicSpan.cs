using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TestRunner;

namespace Tests.Surface
{
	public class AtomicSpan : ITestSurface
	{
		public string Info => "Test concurrent access over the same memory fragment.";
		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IsComplete { get; private set; }
		public bool RequireArgs => false;

		public Task Run(IDictionary<string, List<string>> args)
		{
			const int IN_PARALLEL = 30;
			var T = new Task[IN_PARALLEL];

			using (var hw = new HeapHighway(2000))
			{
				// Alloc 21 ints
				var frag = hw.Alloc((IN_PARALLEL + 1) * 4);

				// Launch multiple tasks
				for (int i = 0; i < IN_PARALLEL; i++)
					T[i] = Task.Factory.StartNew((idx) =>
					{
						var intSpan = MemoryMarshal.Cast<byte, int>(frag.Span());
						int pos = (int)idx;
						var laps = 1; // > 0 just to be sure when checking

						// Await with terrible cache flushing
						while (Interlocked.CompareExchange(ref intSpan[IN_PARALLEL], pos, 0) != 0)
							laps++;

						var posAndLaps = laps + (pos * 10_000_000);

						Volatile.Write(ref intSpan[pos], posAndLaps);

						// Release
						Interlocked.Exchange(ref intSpan[IN_PARALLEL], 0);

					}, i);

				Task.WaitAll(T);

				// Check if all tasks have updated their corresponding cell
				var theSpan = MemoryMarshal.Cast<byte, int>(frag.Span());
				for (int i = 0; i < theSpan.Length - 1; i++)
					if (theSpan[i] < 0)
					{
						Passed = false;
						FailureMessage = $"The Task {i} haven't touched its cell.";
						break;
					}
					else Print.AsInnerInfo($"{i}: {theSpan[i]} laps");

				Passed = true;
				Print.Trace(hw.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);
			}

			return Task.Delay(0);
		}
	}
}
