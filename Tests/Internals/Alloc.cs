/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Internals
{
	public class AllocTestArgs
	{
		public int Count = 1000;
		public int Size = 14000;
		public int InParallel = 1;
		public int AllocDelayMS = 200;
		public int AllocTries = 20;
		public int FragmentDisposeAfterMS = 2000;
		public bool RandomizeLength = true;
		public bool RandomizeFragDisposal = true;
		public bool RandomizeAllocDelay = true;
		public bool AwaitFragmentDisposal = true;
		public int Trace = 1;

		public string FullTrace()
		{
			return FormatText.JoinLines(
				"Allocation settings:",
				$"  Count: {Count}",
				$"  Size: {Size}; IsRandomized: {RandomizeLength}",
				$"  In Parallel: {InParallel}",
				$"  AllocDelay: {AllocDelayMS}ms; IsRandomized: {RandomizeAllocDelay}",
				$"  AllocTries: {AllocTries}",
				$"  AwaitDisposal: {AwaitFragmentDisposal}; {FragmentDisposeAfterMS}ms; IsRandomized: {RandomizeFragDisposal}"
			);
		}
	}

	static class HighwayExt
	{
		public static void AllocAndWait(this IMemoryHighway hw, AllocTestArgs args)
		{
			var rdm = new Random();
			var hwType = hw.GetType().Name;
			var inParallel = args.InParallel > 0 ? args.InParallel : Environment.ProcessorCount;
			var T = new Task[inParallel];
			var part = args.Count / inParallel;
			var rem = args.Count % inParallel;
			var dispCounter = new CountdownEvent(args.Count);

			for (int i = 0; i < inParallel; i++)
				T[i] = Task.Run(async () =>
				 {
					 // [!] Do not try catch

					 // Someone must process the remainder
					 // It's ok to read from L cache if it's 0;
					 var subCount = rem > 0 ? part + Interlocked.Exchange(ref rem, 0) : part;

					 for (int j = 0; j < subCount; j++)
					 {
						 var size = args.RandomizeLength ? rdm.Next(1, args.Size) : args.Size;
						 var allocDelayMS = args.RandomizeAllocDelay && args.AllocDelayMS > 0 ? rdm.Next(0, args.AllocDelayMS) : args.AllocDelayMS;
						 var dispDelayMS = args.RandomizeFragDisposal ? rdm.Next(0, args.FragmentDisposeAfterMS) : args.FragmentDisposeAfterMS;

						 if (allocDelayMS > 0)
							 await Task.Delay(allocDelayMS);

						 var frag = hw.AllocFragment(size, args.AllocTries);

						 if (frag == null)
						 {
							 "failed to allocate a fragment. The Highway is full.".AsInfo(ConsoleColor.DarkMagenta);
							 dispCounter.Signal();
							 continue;
						 }

						 if (args.Trace > 0)
							 Print.Trace("alloc {0,8} bytes on {1} thread: {2} ",
								 ConsoleColor.Magenta, size, hwType,
								 Thread.CurrentThread.ManagedThreadId);

						 var t = Task.Run(async () =>
						 {
							 if (dispDelayMS > 0)
								 await Task.Delay(dispDelayMS);

							 if (args.Trace > 0)
								 Print.Trace("free  {0,8} bytes on {1} thread: {2} ",
								 ConsoleColor.Green, frag.Length, hwType,
								 Thread.CurrentThread.ManagedThreadId);

							 frag.Dispose();
							 dispCounter.Signal();
						 });
					 }
				 });

			Task.WaitAll(T);
			if (args.AwaitFragmentDisposal) dispCounter.Wait();
		}
	}
}
