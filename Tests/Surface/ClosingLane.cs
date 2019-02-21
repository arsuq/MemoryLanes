/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	public class ClosingLane : ITestSurface
	{
		public string Info => "Tests lane skipping.";

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

			var stg = new HighwaySettings(2000, 8, 10000);
			var iH = new Dictionary<string, IMemoryHighway>();

			iH.Add("mh", new HeapHighway(stg, 2000, 2000, 2000));
			iH.Add("nh", new MarshalHighway(stg, 2000, 2000, 2000));
			iH.Add("mmf", new MappedHighway(stg, 2000, 2000, 2000));

			foreach (var kp in iH)
			{
				var hwName = kp.Value.GetType().Name;
				var F = new List<MemoryFragment>();

				if (opt.Contains(kp.Key))
				{
					var hw = kp.Value;
					using (hw)
					{
						var lane1 = hw[1];
						var lane2 = hw[2];
						lane1.Force(true);

						F.Add(hw.AllocFragment(1500));
						F.Add(hw.AllocFragment(1500));

						try
						{
							var af = hw.GetTotalActiveFragments();
							if (af != 2)
							{
								Passed = false;
								FailureMessage = string.Format("{0}: expected 2 active fragments, got {1}", hwName, af);
								return;
							}
							if (lane1.Allocations > 0)
							{
								Passed = false;
								FailureMessage = string.Format("{0}: the lane #1 was force closed, it should have 0 fragments, got {1}", hwName, lane1.Allocations);
								return;
							}
							if (lane2.Allocations < 1)
							{
								Passed = false;
								FailureMessage = string.Format("{0}: the lane #2 should have at least one allocation, found {1}", hwName, lane2.Allocations);
								return;
							}

							$"{hwName}: closing a lane works as expected".AsSuccess();

							lane1.Force(false);
							// should go into lane1 because lane0 has 1500/2000 
							F.Add(hw.AllocFragment(1500));

							if (lane1.Allocations != 1)
							{
								Passed = false;
								FailureMessage = string.Format("{0}: the lane #1 was force opened, it should have 1 fragments, got {1}", hwName, lane1.Allocations);
								return;
							}
						}
						finally
						{
							foreach (var f in F) f.Dispose();
						}

						Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
					}
				}
			}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
