﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestSurface;
using Tests.Internals;

namespace Tests.Surface
{
	public class BeyondCapacity : ITestSurface
	{
		public string Info => "Tests allocation outside the highway capacity.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool RequiresArgs => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			if (args.ContainsKey("-all"))
				args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

			args.AssertAll("-store");
			var opt = args["-store"];
			opt.AssertNothingOutsideThese("mh", "mmf", "nh");

			var allocArgs = new AllocTestArgs()
			{
				Count = 5,
				Size = 5000_000,
				InParallel = 4,
				RandomizeAllocDelay = true,
				RandomizeFragDisposal = false,
				RandomizeLength = false,
				AllocDelayMS = 0,
				FragmentDisposeAfterMS = 4000 // long enough
			};

			if (args.ContainsKey("-count")) allocArgs.Count = int.Parse(args["-count"][0]);
			if (args.ContainsKey("-size")) allocArgs.Count = int.Parse(args["-size"][0]);

			var defHwCap = MemoryLaneSettings.DEF_LANE_CAPACITY * 1.5;

			if (allocArgs.Count * allocArgs.Size < defHwCap)
			{
				Passed = false;
				FailureMessage = "The default highway capacity can handle all fragments. Should test out of the capacity bounds.";
				return;
			}

			Print.Trace(allocArgs.FullTrace(), ConsoleColor.Cyan, ConsoleColor.Black, null);

			if (opt.Contains("mh"))
				using (var hw = new HeapHighway())
				{
					hw.AllocAndWait(allocArgs);
					if (hw.GetTotalActiveFragments() > 0)
					{
						Passed = false;
						FailureMessage = "The HeapHighway has active fragments after the AllocAndWait()";
						return;
					}
					if (hw.GetLanesCount() < 3)
					{
						Passed = false;
						FailureMessage = "The HeapHighway has less than 3 lanes. ";
						return;
					}
					Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
				}

			if (opt.Contains("nh"))
				using (var hw = new MarshalHighway())
				{
					hw.AllocAndWait(allocArgs);
					if (hw.GetTotalActiveFragments() > 0)
					{
						Passed = false;
						FailureMessage = "The MarshalHighway has active fragments after the AllocAndWait()";
						return;
					}
					if (hw.GetLanesCount() < 3)
					{
						Passed = false;
						FailureMessage = "The HeapHighway has less than 3 lanes. ";
						return;
					}
					Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
				}

			if (opt.Contains("mmf"))
				using (var hw = new MappedHighway())
				{
					hw.AllocAndWait(allocArgs);
					if (hw.GetTotalActiveFragments() > 0)
					{
						Passed = false;
						FailureMessage = "The MappedHighway has active fragments after the AllocAndWait()";
						return;
					}
					if (hw.GetLanesCount() < 3)
					{
						Passed = false;
						FailureMessage = "The HeapHighway has less than 3 lanes. ";
						return;
					}
					Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
				}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
