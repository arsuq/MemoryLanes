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
			"Test normal allocation, i.e. without reaching the MaxLaneCount or MaxTotalBytes limits.",
			"Flags: -store",
			"Arguments: one or many [mh = managed heap, mmf = memory mapped file, nh = native heap]," +
			"Optional Flags: -count = fragments count, -size = fragment size, which will be randomized" +
			"Example: -ExpectedAllocation -store mh nh mmf");

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool RequireArgs => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			args.AssertAll("-store");
			var opt = args["-store"];
			opt.AssertNothingButTheseArgs("mh", "mmf", "nh");

			var allocArgs = new AllocTestArgs()
			{
				Count = 200,
				Size = 18000,
				InParallel = -1,
				RandomizeAllocDelay = true,
				RandomizeFragDisposal = true,
				RandomizeLength = true,
				AllocDelayMS = 400,
				FragmentDisposeAfterMS = 700
			};

			if (args.ContainsKey("-count")) allocArgs.Count = int.Parse(args["-count"][0]);
			if (args.ContainsKey("-size")) allocArgs.Count = int.Parse(args["-size"][0]);

			if (allocArgs.Count * allocArgs.Size > 12_000_000)
			{
				Passed = false;
				FailureMessage = "The default highway capacity is not enough if all fragments live forever.";
				return;
			}

			if (opt.Contains("mh"))
				using (var hw = new HeapHighway())
				{
					hw.AllocAndWait(allocArgs);
					if (hw.GetTotalActiveFragments() > 0)
					{
						Passed = false;
						FailureMessage = "The HeapHighway has active fragments after the AllocAndWait()";
					}
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
				}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
