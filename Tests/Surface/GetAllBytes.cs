/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	public class GetAllBytes : ITestSurface
	{
		public string Info => "Tests the GetAllBytes lane method. This should be used for diagnostics only. Args: -store mh mmf nh";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public Task Run(IDictionary<string, List<string>> args)
		{
			if (args.ContainsKey("+all"))
				args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

			args.AssertAll("-store");
			var opt = args["-store"];
			opt.AssertNothingOutsideThese("mh", "mmf", "nh");

			const int LANE_SIZE = 100;
			const int ALLOC_SIZE = 25;

			var stg = new HighwaySettings(LANE_SIZE, 1);
			var iH = new Dictionary<string, IMemoryHighway>();

			iH.Add("nh", new MarshalHighway(stg, stg.DefaultCapacity));
			iH.Add("mh", new HeapHighway(stg, stg.DefaultCapacity));
			iH.Add("mmf", new MappedHighway(stg, stg.DefaultCapacity));

			var BYTES = new byte[LANE_SIZE];

			foreach (var kp in iH)
				if (opt.Contains(kp.Key))
				{
					using (var hw = kp.Value)
					{
						var hwName = hw.GetType().Name;

						for (int i = 0; i < LANE_SIZE / ALLOC_SIZE; i++)
						{
							var f = hw.AllocFragment(ALLOC_SIZE);
							for (int j = 0; j < f.Length; j++)
							{
								var b = (byte)i;
								f.Write(b, j);
								BYTES[i * ALLOC_SIZE + j] = b;
							}
						}

						var bytes = hw[0].GetAllBytes();

						if (bytes.Length != BYTES.Length)
						{
							FailureMessage = $"{hwName} GetAllBytes returned span with unexpected length";
							Passed = false;
							break;
						}

						for (int i = 0; i < bytes.Length; i++)
							if (BYTES[i] != bytes[i])
							{
								FailureMessage = $"{hwName} GetAllBytes returned span with incorrect data";
								Passed = false;
								return Task.CompletedTask;
							}
					}
				}

			Passed = true;
			IsComplete = true;

			return Task.CompletedTask;
		}
	}
}