/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	public class ReopenLane : ITestSurface
	{
		public string Info => "Tests lane creation at highway index. Args: -store mh mmf nh";
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

			const int MAX_LANES = 4;
			const int LANE_SIZE = 100;
			const int ALLOC_SIZE = 25;
			const int TARGET_LANE_IDX = 1;

			var stg = new HighwaySettings(LANE_SIZE, MAX_LANES);
			var iH = new Dictionary<string, IMemoryHighway>();

			iH.Add("nh", new MarshalHighway(stg, stg.DefaultCapacity));
			iH.Add("mh", new HeapHighway(stg, stg.DefaultCapacity));
			iH.Add("mmf", new MappedHighway(stg, stg.DefaultCapacity));

			foreach (var kp in iH)
				if (opt.Contains(kp.Key))
				{
					using (var hw = kp.Value)
					{
						var hwName = hw.GetType().Name;

						for (int i = 0; i < MAX_LANES * 4; i++)
							hw.AllocFragment(ALLOC_SIZE);

						if (hw.ReopenLane(TARGET_LANE_IDX) != null)
						{
							Passed = false;
							FailureMessage = $"{hwName}: ReopenLane returned a non disposed lane.";
							return;
						}

						$"Disposing a lane".AsInfo();
						hw.DisposeLane(TARGET_LANE_IDX);

						if (hw.ReopenLane(TARGET_LANE_IDX) == null)
						{
							Passed = false;
							FailureMessage = $"{hwName}: ReopenLane failed to create a lane.";
							return;
						}

						for (int i = 0; i < 4; i++)
							hw.AllocFragment(ALLOC_SIZE);

						var tfc = hw.GetTotalActiveFragments();
						var allocs = hw[TARGET_LANE_IDX].Allocations;

						if (tfc != MAX_LANES * 4)
						{
							Passed = false;
							FailureMessage = $"{hwName}: The reopened lane was not used for allocations as expected.";
							return;
						}

						if (allocs != 4)
						{
							Passed = false;
							FailureMessage = $"{hwName}: The highway has {allocs} lanes; expected {LANE_SIZE / ALLOC_SIZE}";
							return;
						}

						$"OK: {hwName} successfully reopens a lane.".AsSuccess();
					}
				}

			Passed = true;
			IsComplete = true;
		}
	}
}