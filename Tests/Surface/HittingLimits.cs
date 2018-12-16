using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestRunner;
using Tests.Internals;

namespace Tests.Surface
{
	public class HittingLimits : ITestSurface
	{
		public string Info => "Tests the handling the MaxTotalAllocatedBytes and MaxLanesCount thresholds.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool RequireArgs => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			if (args.ContainsKey("-all"))
				args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

			args.AssertAll("-store");
			var opt = args["-store"];
			opt.AssertNothingButTheseArgs("mh", "mmf", "nh");

			var allocArgs = new AllocTestArgs()
			{
				Count = 5,
				Size = 5000_000,
				InParallel = 2,
				RandomizeAllocDelay = true,
				RandomizeFragDisposal = false,
				RandomizeLength = false,
				AwaitFragmentDisposal = true,
				AllocDelayMS = 0,
				FragmentDisposeAfterMS = 2000 // keep them alive
			};

			if (allocArgs.Count * allocArgs.Size < 12_000_000)
			{
				Passed = false;
				FailureMessage = "The default highway capacity can handle all fragments. Should test out of the capacity bounds.";
				return;
			}

			Print.Trace(allocArgs.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);

			var stg_ignore = new MemoryLaneSettings(8_000_000, 2, 10_000_000);
			var stg_throw = new MemoryLaneSettings(8_000_000, 2, 10_000_000);

			stg_ignore.OnMaxLaneReached = () =>
			{
				Print.AsInnerInfo("OnMaxLaneReached(), allowing it to continue. ");
				return true;
			};

			stg_ignore.OnMaxTotalBytesReached = () =>
			{
				Print.AsInnerInfo("OnMaxTotalBytesReached(), ignoring. ");
				return true;
			};

			var iH = new Dictionary<string, IMemoryHighway>();

			iH.Add("mh", new HeapHighway(stg_ignore));
			iH.Add("nh", new MarshalHighway(stg_ignore));
			iH.Add("mmf", new MappedHighway(stg_ignore));

			var dH = new Dictionary<string, IMemoryHighway>();

			dH.Add("mh", new HeapHighway(stg_throw));
			dH.Add("nh", new MarshalHighway(stg_throw));
			dH.Add("mmf", new MappedHighway(stg_throw));

			// The ignoring case
			foreach (var kp in iH)
				if (opt.Contains(kp.Key))
				{
					var hw = kp.Value;
					var hwName = hw.GetType().Name;
					using (hw)
					{
						hw.AllocAndWait(allocArgs);
						if (hw.GetTotalActiveFragments() > 0)
						{
							Passed = false;
							FailureMessage = string.Format("The {0} has active fragments after the AllocAndWait()", hw.GetType().Name);
							return;
						}
						if (hw.GetLanesCount() < 3)
						{
							Passed = false;
							FailureMessage = string.Format("The {0} has less than 3 lanes. ", hwName);
							return;
						}
						Print.Trace(hw.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);
					}
				}

			// The default case: throws MemoryLaneException
			foreach (var kp in dH)
				if (opt.Contains(kp.Key))
				{
					var hw = kp.Value;
					var hwName = hw.GetType().Name;
					using (hw)
					{
						try { hw.AllocAndWait(allocArgs); }
						catch (AggregateException aggr)
						{
							Interlocked.Exchange(ref allocArgs.Trace, 0);

							foreach (var ex in aggr.Flatten().InnerExceptions)
							{
								var mex = ex as MemoryLaneException;

								if (mex != null)
								{
									if (mex.ErrorCode != MemoryLaneException.Code.MaxLanesCountReached &&
										mex.ErrorCode != MemoryLaneException.Code.MaxTotalAllocBytesReached)
									{
										Passed = false;
										FailureMessage = string.Format(
											"The {0} should have failed with MaxLanesCountReached or MaxTotalAllocBytesReached",
											hwName);
										return;
									}

									Print.AsInnerInfo("{0} in {1} as expected", mex.ErrorCode, hwName);
								}
								else
								{
									Passed = false;
									FailureMessage = ex.Message;
									return;
								}
							}
						}
						catch (Exception ex)
						{
							Passed = false;
							FailureMessage = ex.Message;
							return;
						}

						if (hw.GetLanesCount() > 3)
						{
							Passed = false;
							FailureMessage = string.Format("The {0} has more than 3 lanes, should have failed. ", hw.GetType().Name);
							return;
						}

						Print.Trace(hw.FullTrace(4), ConsoleColor.Cyan, ConsoleColor.Black, null);
					}
				}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
