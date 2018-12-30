using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestRunner;

namespace Tests.Surface
{
	public class GhostTrackingLimit : ITestSurface
	{
		public string Info => $"Asserts that lanes stop allocating fragments when the ghost-tracking capacity is reached.";

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

			if (!skipLaneWhenMaxedOut(opt)) return;

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}

		bool skipLaneWhenMaxedOut(List<string> opt)
		{
			var stg = new MemoryLaneSettings(1024, 2, MemoryLane.DisposalMode.TrackGhosts);
			var iH = new Dictionary<string, IMemoryHighway>();

			var LEN = stg.DefaultCapacity;

			iH.Add("mh", new HeapHighway(stg, LEN, LEN));
			iH.Add("nh", new MarshalHighway(stg, LEN, LEN));
			iH.Add("mmf", new MappedHighway(stg, LEN, LEN));

			foreach (var kp in iH)
			{
				var hwName = kp.Value.GetType().Name;
				var F = new List<MemoryFragment>();

				if (opt.Contains(kp.Key))
				{
					var hw = kp.Value;
					using (hw)
					{
						var maxTrackingCap = LEN / MemoryLane.TRACKER_ASSUMED_MIN_FRAG_SIZE_BYTES;
						var half = MemoryLane.TRACKER_ASSUMED_MIN_FRAG_SIZE_BYTES / 2;

						$"Allocating twice the tracking capacity ({maxTrackingCap}) of the lane with 16 byte slices.".Trace(ConsoleColor.Magenta);

						// Half of them should go to lane 1
						for (int i = 0; i < maxTrackingCap * 2; i++)
							hw.AllocFragment(half);

						var slots = 1 + (int)Math.Sqrt(maxTrackingCap);
						slots *= slots;

						if (hw[0].Allocations > slots)
						{
							Passed = false;
							FailureMessage = $"{hwName}: expected the lane to stop allocating for lack of tracking slots. " +
								$"It has {hw[0].Allocations} allocations and {maxTrackingCap} tracking capacity";
						}

						$"{hwName}: lane 0 has {hw[0].Allocations} allocations as expected.".AsSuccess();

						if (hw[1].Allocations < (maxTrackingCap * 2) - slots)
						{
							Passed = false;
							FailureMessage = $"{hwName}: expected the rejected fragments from lane 0 to be successfully allocated on lane 1. " +
								$"Lane 1 has {hw[1].Allocations} allocations";
						}

						$"{hwName}: lane 1 took {hw[1].Allocations} rejected fragments as expected.".AsSuccess();

						Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
					}
				}
			}

			return true;
		}
	}
}
