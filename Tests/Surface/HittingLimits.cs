/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestSurface;
using Tests.Internals;

namespace Tests.Surface
{
	public class HittingLimits : ITestSurface
	{
		public string Info => "Tests the handling the MaxTotalAllocatedBytes and MaxLanesCount thresholds. Args: -store mh mmf nh";

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

			var allocArgs = new AllocTestArgs()
			{
				Count = 5,
				Size = 5_000_000,
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

			Print.Trace(allocArgs.FullTrace(), ConsoleColor.Cyan, ConsoleColor.Black, null);

			var stg_ignore = new HighwaySettings(8_000_000, 2, 10_000_000);
			var stg_throw = new HighwaySettings(8_000_000, 2, 10_000_000);

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
			try
			{
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
								FailureMessage = $"The {hwName} has active fragments after the AllocAndWait()";
								return;
							}
							if (hw.GetLanesCount() > stg_ignore.MaxLanesCount)
							{
								Passed = false;
								FailureMessage = $"The {hwName} has more than {stg_ignore.MaxLanesCount} lanes.";
								return;
							}
							Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
						}
					}

				"The limits ignoring case.".AsSuccess();

				// The default case: throws MemoryLaneExceptions
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

										$"{mex.ErrorCode} in {hwName} as expected".AsSuccess();
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

							if (hw.GetLanesCount() > stg_throw.MaxLanesCount)
							{
								Passed = false;
								FailureMessage = $"The {hwName} has more than {stg_throw.MaxLanesCount} lanes, should have failed. ";
								return;
							}

							Print.Trace(hw.FullTrace(), 2, true, ConsoleColor.Cyan, ConsoleColor.Black, null);
						}
					}

				"The default - throwing exceptions case.".AsSuccess();
			}
			catch (Exception ex)
			{
				Passed = false;
				FailureMessage = ex.Message;
				return;
			}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
