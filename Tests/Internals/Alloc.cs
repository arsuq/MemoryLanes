using System;
using System.Threading;
using System.Threading.Tasks;
using TestRunner;

namespace Tests.Internals
{
	public class AllocTestArgs
	{
		public int Count = 1000;
		public int Size = 14000;
		public int InParallel = 1;
		public int AllocDelayMS = 200;
		public int FragmentDisposeAfterMS = 2000;
		public bool RandomizeLength = true;
		public bool RandomizeFragDisposal = true;
		public bool RandomizeAllocDelay = true;
	}

	static class HighwayExt
	{
		public static void AllocAndWait(this IHighway hw, AllocTestArgs args)
		{
			var rdm = new Random();
			var hwType = hw.GetType().Name;

			Parallel.For(0, args.Count, new ParallelOptions() { MaxDegreeOfParallelism = args.InParallel }, (i) =>
			{
				var size = args.RandomizeLength ? rdm.Next(1, args.Size) : args.Size;
				var allocDelayMS = args.RandomizeAllocDelay ? rdm.Next(0, args.AllocDelayMS) : args.AllocDelayMS;
				var dispDelayMS = args.RandomizeFragDisposal ? rdm.Next(0, args.FragmentDisposeAfterMS) : args.FragmentDisposeAfterMS;

				Thread.Sleep(allocDelayMS);

				var frag = hw.AllocFragment(size);
				Print.Trace("    alloc {0,8} bytes on {1} thread: {2} ", ConsoleColor.Magenta, null, size, hwType, Thread.CurrentThread.ManagedThreadId);

				Task.Run(() =>
				{
					Thread.Sleep(dispDelayMS);
					Print.Trace("    free  {0,8} bytes on {1} thread: {2} ", ConsoleColor.Green, null, frag.Length, hwType, Thread.CurrentThread.ManagedThreadId);
					frag.Dispose();
				}).Wait();
			});
		}
	}
}
