/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestSurface;
using Tests.Internals;

namespace Tests.Surface
{
	public class ExpectedAllocation : ITestSurface
	{
		public string Info => FormatText.JoinLines(
			"Tests the trivial allocation, i.e. without reaching the MaxLaneCount or MaxTotalBytes limits.",
			"Flags: -store",
			"Args: one or many [mh = managed heap, mmf = memory mapped file, nh = native heap]," +
			"Optional Flags: -count = fragments count, -size = fragment size, which will be randomized" +
			"Example: -ExpectedAllocation -store mh nh mmf");

		public string Tags => string.Empty;
		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public async Task Start(IDictionary<string, List<string>> args)
		{
			await Task.Delay(0);

			if (args.ContainsKey("+all"))
				args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

			args.AssertAll("-store");
			var opt = args["-store"];
			opt.AssertNothingOutsideThese("mh", "mmf", "nh");

			var allocArgs = new AllocTestArgs()
			{
				Count = 20,
				Size = 180000,
				InParallel = 10,
				RandomizeAllocDelay = true,
				RandomizeFragDisposal = true,
				RandomizeLength = true,
				AllocDelayMS = 0,
				AllocTries = 1,
				FragmentDisposeAfterMS = 60
			};

			if (args.ContainsKey("-count")) allocArgs.Count = int.Parse(args["-count"][0]);
			if (args.ContainsKey("-size")) allocArgs.Size = int.Parse(args["-size"][0]);

			if (allocArgs.Count * allocArgs.Size > 12_000_000)
			{
				Passed = false;
				FailureMessage = "The default highway capacity is not enough if all fragments live forever.";
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
					}
					Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
				}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
