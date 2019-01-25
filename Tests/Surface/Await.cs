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
	public class Await : ITestSurface
	{
		public string Info => "Tests the no await behavior of the Alloc() method.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
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
				Count = 6000,
				Size = 1200,
				InParallel = 12,
				RandomizeAllocDelay = false,
				RandomizeFragDisposal = true,
				RandomizeLength = false,
				AllocDelayMS = 10,
				AllocLockAwaitMs = -1,
				AwaitFragmentDisposal = false,
				FragmentDisposeAfterMS = 100,
				Trace = 0
			};

			var H = new Dictionary<string, IMemoryHighway>();
			var ms = new MemoryLaneSettings(300_000, 300, 700_000_000);
			var lanes = new int[10];
			Array.Fill(lanes, 300_000);

			H.Add("mh", new HeapHighway(ms, lanes));
			H.Add("nh", new MarshalHighway(ms, lanes));
			H.Add("mmf", new MappedHighway(ms, lanes));

			Print.Trace(allocArgs.FullTrace(), ConsoleColor.Cyan, ConsoleColor.Black, null);

			foreach (var kp in H)
				if (opt.Contains(kp.Key))
				{
					var hw = kp.Value;
					var hwName = hw.GetType().Name;

					using (hw)
					{
						hw.AllocAndWait(allocArgs);
						var fragsCount = hw.GetTotalActiveFragments();

						if (fragsCount < args.Count)
						{
							Passed = false;
							FailureMessage = string.Format(
								"{0}: Failed to allocate all {1} fragments, got {2}.",
								hwName, allocArgs.Count, fragsCount);

							return;
						}

						Print.AsInnerInfo(
								"{0}: Total lanes count: {1} Total active fragments: {2}",
								hwName, hw.GetLanesCount(), hw.GetTotalActiveFragments());

						Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
					}
				}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
