/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	class NoLuckGate : ITestSurface
	{
		public string Info => "Tests the highway concurrent lane allocation behavior. Args: -store mh mmf nh";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			if (args.ContainsKey("+all"))
				args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

			args.AssertAll("-store");
			var opt = args["-store"];
			opt.AssertNothingOutsideThese("mh", "mmf", "nh");

			const int ALLOC_SIZE = 100;
			const int FRAGS_COUNT = 100;
			var ccAllocs = new int[3] { 1, 2, 3 };

			foreach (var cc in ccAllocs)
			{
				// The initial hw space will fit half of the fragments
				var stg = new HighwaySettings((FRAGS_COUNT / 2) * ALLOC_SIZE);
				var iH = new Dictionary<string, IMemoryHighway>();

				// Set the no-luckGate capacity
				stg.ConcurrentNewLaneAllocations = cc;

				iH.Add("nh", new MarshalHighway(stg, stg.DefaultCapacity));
				iH.Add("mh", new HeapHighway(stg, stg.DefaultCapacity));
				iH.Add("mmf", new MappedHighway(stg, stg.DefaultCapacity));

				foreach (var kp in iH)
					if (opt.Contains(kp.Key))
					{
						var CCAllocs = new Task[FRAGS_COUNT];
						var frags = new MemoryFragment[FRAGS_COUNT];

						using (var hw = kp.Value)
						{
							var hwName = hw.GetType().Name;

							for (int i = 0; i < CCAllocs.Length; i++)
								CCAllocs[i] = new Task((idx) => frags[(int)idx] = hw.AllocFragment(ALLOC_SIZE), i);

							$"Starting all {CCAllocs.Length} concurrent allocations".AsInfo();

							for (int i = 0; i < CCAllocs.Length; i++)
								CCAllocs[i].Start();

							Task.WaitAll(CCAllocs);

							"Allocs complete".AsInfo();

							if (hw.GetLanesCount() - 1 > cc)
							{
								Passed = false;
								FailureMessage = $"{hwName}: There are more than {cc} new lanes. Expected the no-luckGate to hold them off.";
								return;
							}

							var nullFrags = frags.Where(x => x == null).Count();

							if (nullFrags > 0)
							{
								Passed = false;
								FailureMessage = $"{hwName}: There are {nullFrags} null fragments. The ConcurrentNewLaneAllocations is {cc}.";
								return;
							}

							$"{hwName}: Correctly allocated exactly {cc} new lanes.".AsSuccess();
						}
					}
			}

			Passed = true;
			IsComplete = true;
		}
	}
}
