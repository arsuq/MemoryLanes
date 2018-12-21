using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestRunner;

namespace Tests.Surface
{
	public class AtomicSpan : ITestSurface
	{
		public string Info =>
			"Tests concurrent access over the same memory fragment casted as integer slice." +
			"This test is not about MemoryLanes, but rather about interpretation " +
			"of byte segments to different structure layouts;";
		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IsComplete { get; private set; }
		public bool RequiresArgs => false;

		public Task Run(IDictionary<string, List<string>> args)
		{
			const int IN_PARALLEL = 30;
			var T = new Task[IN_PARALLEL];

			using (var hw = new HeapHighway(2000))
			{
				// Alloc +1 ints, the last cell will be concurrently read/written by each task. 
				// When the task id is successfully stored, the task is allowed to update its on cell.
				var frag = hw.Alloc((IN_PARALLEL + 1) * 4);

				// Launch multiple tasks
				for (int i = 0; i < IN_PARALLEL; i++)
					T[i] = Task.Factory.StartNew((idx) =>
					{
						var intSpan = frag.ToSpan<int>();
						int pos = (int)idx;
						var laps = 0;

						// Await with terrible cache flushing
						while (Interlocked.CompareExchange(ref intSpan[IN_PARALLEL], pos, 0) != 0)
							laps++;

						var posAndLaps = laps + (pos * 10_000_000);

						Volatile.Write(ref intSpan[pos], posAndLaps);

						// Release
						Interlocked.Exchange(ref intSpan[IN_PARALLEL], 0);

					}, i);

				Task.WaitAll(T);

				// Check if all tasks have updated their corresponding cells
				var theSpan = frag.ToSpan<int>();
				for (int i = 0; i < theSpan.Length - 1; i++)
					if (theSpan[i] < 0)
					{
						Passed = false;
						FailureMessage = $"The Task {i} hasn't touched its cell.";
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
