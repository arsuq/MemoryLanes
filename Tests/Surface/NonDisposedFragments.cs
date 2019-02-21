/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	public class NonDisposedFragments : ITestSurface
	{
		public string Info => "Tests the Fragments' destructor.";

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

			if (!reset(opt)) return;
			if (!freeGhosts(opt)) return;

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}

		bool reset(List<string> opt)
		{
			var stg = new HighwaySettings(4000, 8, 10000);
			var iH = new Dictionary<string, IMemoryHighway>();

			iH.Add("mh", new HeapHighway(stg, 4000));
			iH.Add("nh", new MarshalHighway(stg, 4000));
			iH.Add("mmf", new MappedHighway(stg, 4000));

			foreach (var kp in iH)
			{
				var hwName = kp.Value.GetType().Name;
				var F = new List<MemoryFragment>();

				if (opt.Contains(kp.Key))
				{
					var hw = kp.Value;
					using (hw)
					{
						F.Add(hw.AllocFragment(500));
						F.Add(hw.AllocFragment(500));
						F.Add(hw.AllocFragment(500));
						hw.AllocFragment(1000); // lost  
						F.Add(hw.AllocFragment(500));
						F.Add(hw.AllocFragment(1000));

						foreach (var f in F) f.Dispose();

						var af = hw.GetTotalActiveFragments();
						if (af == 1)
						{
							Print.AsInnerInfo("{0} has {1} non disposed fragments", hwName, af);
							af = hw.GetTotalActiveFragments();

							if (af != 1)
							{
								Passed = false;
								FailureMessage = string.Format("{0}: expected one ghost fragment, found {1}.", hwName, af);
								return false;
							}

							Print.Trace("Forcing reset.. ", ConsoleColor.Magenta, hwName, af);
							var lane0 = hw[0];
							lane0.Force(false, true);
							af = hw.GetTotalActiveFragments();


							if (af != 0)
							{
								Passed = false;
								FailureMessage = string.Format("{0}: expected 0 ghost fragments after forcing a reset, found {1}.", hwName, af);
								return false;
							}
							else Print.Trace("{0} has {1} allocations and offset {2}", ConsoleColor.Green, hwName, lane0.Allocations, lane0.Offset);
						}
						else
						{
							Passed = false;
							FailureMessage = string.Format("{0}: the active fragments count is wrong, should be 1.", hwName);
							return false;
						}

						Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black);
					}
				}
			}

			return true;
		}

		bool freeGhosts(List<string> opt)
		{
			var stg = new HighwaySettings(1024, 3, MemoryLaneResetMode.TrackGhosts);
			var iH = new Dictionary<string, IMemoryHighway>();

			iH.Add("mh", new HeapHighway(stg, 1024, 1024));
			iH.Add("nh", new MarshalHighway(stg, 1024, 1024));
			iH.Add("mmf", new MappedHighway(stg, 1024, 1024));

			foreach (var kp in iH)
			{
				var hwName = kp.Value.GetType().Name;
				var F = new List<MemoryFragment>();

				if (opt.Contains(kp.Key))
				{
					var hw = kp.Value;
					using (hw)
					{
						Print.Trace("Allocating 4 fragments 1 will be lost.", ConsoleColor.Magenta);

						void alloc()
						{
							F.Add(hw.AllocFragment(500));
							hw.AllocFragment(500); // lost  
							F.Add(hw.AllocFragment(500));
							F.Add(hw.AllocFragment(500));
						}

						alloc();

						Print.Trace("Triggering FreeGhosts()", ConsoleColor.Magenta);

						hw.FreeGhosts();
						var af = hw.GetTotalActiveFragments();

						if (af != 4)
						{
							Passed = false;
							FailureMessage = $"{hwName} FreeGhosts() collected something, maybe the GC passed. This may not be a failure.";
							return false;
						}

						"FreeGhosts() did nothing on the live fragments".AsSuccess();
						"Forcing GC and then FreeGhosts() ".Trace(ConsoleColor.Magenta);

						GC.Collect(2);
						GC.WaitForFullGCComplete();
						Thread.Sleep(3000);
						hw.FreeGhosts();
						af = hw.GetTotalActiveFragments();

						if (af < 3)
						{
							Passed = false;
							FailureMessage = $"{hwName} expected 3 active fragments after one GC and GreeGhosts, got {af}";
							return false;
						}

						"FreeGhosts() collected 1 as expected".AsSuccess();
						"Disposing all".Trace(ConsoleColor.Magenta);

						foreach (var f in F) f.Dispose();

						af = hw.GetTotalActiveFragments();

						if (af > 0)
						{
							Passed = false;
							FailureMessage = $"{hwName} lane resetting on dispose is failing";
							return false;
						}

						"FreeGhosts()".AsSuccess();

						Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
					}
				}
			}

			return true;
		}
	}
}
