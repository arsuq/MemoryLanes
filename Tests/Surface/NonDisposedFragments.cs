using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestRunner;

namespace Tests.Surface
{
	public class NonDisposedFragments : ITestSurface
	{
		public string Info => "Tests the Fragments' destructor.";

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

			var stg = new MemoryLaneSettings(4000, 8, 10000);
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
								return;
							}

							Print.Trace("    Forcing reset.. ", ConsoleColor.Magenta, null, hwName, af);
							var lane0 = hw[0];
							lane0.Force(false, true);
							af = hw.GetTotalActiveFragments();


							if (af != 0)
							{
								Passed = false;
								FailureMessage = string.Format("{0}: expected 0 ghost fragments after forcing a reset, found {1}.", hwName, af);
								return;
							}
							else Print.Trace("    {0} has {1} allocations and offset {2}", ConsoleColor.Green, null, hwName, lane0.Allocations, lane0.Offset);
						}
						else
						{
							Passed = false;
							FailureMessage = string.Format("{0}: the active fragments count is wrong, should be 1.", hwName);
							return;
						}

						Print.Trace(hw.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);
					}
				}
			}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
