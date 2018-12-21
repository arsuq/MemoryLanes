using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestRunner;
using Tests.Internals;

namespace Tests.Surface
{
	public class ExpectedAllocation : ITestSurface
	{
		public string Info => FormatText.JoinLines(
			"Tests the trivial allocation, i.e. without reaching the MaxLaneCount or MaxTotalBytes limits.",
			"Flags: -store",
			"Arguments: one or many [mh = managed heap, mmf = memory mapped file, nh = native heap]," +
			"Optional Flags: -count = fragments count, -size = fragment size, which will be randomized" +
			"Example: -ExpectedAllocation -store mh nh mmf");

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
			opt.AssertNothingButTheseArgs("mh", "mmf", "nh");

			var allocArgs = new AllocTestArgs()
			{
				Count = 20,
				Size = 180000,
				InParallel = 10,
				RandomizeAllocDelay = true,
				RandomizeFragDisposal = true,
				RandomizeLength = true,
				AllocDelayMS = 0,
				FragmentDisposeAfterMS = 60
			};

			if (args.ContainsKey("-count")) allocArgs.Count = int.Parse(args["-count"][0]);
			if (args.ContainsKey("-size")) allocArgs.Count = int.Parse(args["-size"][0]);

			if (allocArgs.Count * allocArgs.Size > 12_000_000)
			{
				Passed = false;
				FailureMessage = "The default highway capacity is not enough if all fragments live forever.";
				return;
			}

			Print.Trace(allocArgs.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);

			if (opt.Contains("mh"))
				using (var hw = new HeapHighway())
				{
					hw.AllocAndWait(allocArgs);
					if (hw.GetTotalActiveFragments() > 0)
					{
						Passed = false;
						FailureMessage = "The HeapHighway has active fragments after the AllocAndWait()";
					}
					Print.Trace(hw.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);
				}

			if (opt.Contains("nh"))
				using (var hw = new MarshalHighway())
				{
					hw.AllocAndWait(allocArgs);
					if (hw.GetTotalActiveFragments() > 0)
					{
						Passed = false;
						FailureMessage = "The MarshalHighway has active fragments after the AllocAndWait()";
					}
					Print.Trace(hw.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);
				}

			if (opt.Contains("mmf"))
				using (var hw = new MappedHighway())
				{
					hw.AllocAndWait(allocArgs);
					if (hw.GetTotalActiveFragments() > 0)
					{
						Passed = false;
						FailureMessage = "The MappedHighway has active fragments after the AllocAndWait()";
					}
					Print.Trace(hw.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);
				}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
