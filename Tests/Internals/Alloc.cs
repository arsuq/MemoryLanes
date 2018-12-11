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
		static void AllocImpl<L, F>(this MemoryCarriage<L, F> hw, AllocTestArgs args) where L : MemoryLane where F : MemoryFragment, new()
		{
			var rdm = new Random();
			var hwType = hw.GetType().Name;

			Parallel.For(0, args.Count, (i) =>
			{
				var size = args.RandomizeLength ? rdm.Next(1, args.Size) : args.Size;
				var allocDelayMS = args.RandomizeAllocDelay ? rdm.Next(0, args.AllocDelayMS) : 200;
				var dispDelayMS = args.RandomizeFragDisposal ? rdm.Next(0, args.FragmentDisposeAfterMS) : 200;

				Task.Delay(allocDelayMS);

				var frag = hw.Alloc(size);
				Print.Trace("    alloc {0,8} bytes on {1} thread: {2} ", ConsoleColor.Magenta, null, size, hwType, Thread.CurrentThread.ManagedThreadId);

				Task.Run(() =>
				{
					Task.Delay(dispDelayMS);
					Print.Trace("    free  {0,8} bytes on {1} thread: {2} ", ConsoleColor.Green, null, frag.Length, hwType, Thread.CurrentThread.ManagedThreadId);
					frag.Dispose();
				}).Wait();
			});
		}

		public static void AllocAndWait(this HeapHighway hw, AllocTestArgs args) => AllocImpl(hw, args);
		public static void AllocAndWait(this MarshalHighway hw, AllocTestArgs args) => AllocImpl(hw, args);
		public static void AllocAndWait(this MappedHighway hw, AllocTestArgs args) => AllocImpl(hw, args);
	}
}
