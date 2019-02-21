/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using TestSurface;

//namespace Tests.Surface
//{
//	public class ForcedDisposal : ITestSurface
//	{
//		public string Info => "Tests manual lane resetting for lost non-disposed fragments.";

//		public string FailureMessage { get; private set; }
//		public bool? Passed { get; private set; }
//		public bool IndependentLaunchOnly => false;
//		public bool IsComplete { get; private set; }

//		public async Task Run(IDictionary<string, List<string>> args)
//		{
//			if (args.ContainsKey("-all"))
//				args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

//			args.AssertAll("-store");
//			var opt = args["-store"];
//			opt.AssertNothingOutsideThese("mh", "mmf", "nh");

//			var ms = new HighwaySettings(1024, 1, 1024);

//			// Will allocate 100 fragments with WeakRef tracking and will dispose half of them,
//			// the other half will be manually reset by calling lane.ResetOne().
//			// Note that the correct lane and cycle must be remembered.
//			void allocAndManualReset(IMemoryHighway hw)
//			{
//				var F = new List<MemoryFragment>();
//				var WR = new List<(WeakReference<MemoryFragment> wfrag, MemoryLane lane, long cycle)>();

//				for (int i = 0; i < 100; i++)
//				{
//					var f = hw.AllocFragment(4);

//					// All fragments are tracked by user code somewhere.
//					WR.Add((new WeakReference<MemoryFragment>(f, false), f.Lane, f.LaneCycle));

//					// Lose half of them, keep the other half to trigger expected disposal.
//					if (i % 2 != 0) F.Add(f);
//				}

//				// In reality this will happen in random points in time
//				for (var i = 0; i < F.Count; i++)
//					F[i].Dispose();

//				// Collect all but F's
//				GC.Collect(2);

//				// This count will never go down automatically
//				if (hw.GetTotalActiveFragments() != 50)
//				{
//					Passed = false;
//					FailureMessage = $"{hw.GetType().Name}: wrong number of fragments after half disposal";
//					return;
//				}

//				// To double check
//				int disposedButNotNull = 0;

//				// Check for ghost fragments
//				foreach (var gf in WR)
//					if (!gf.wfrag.TryGetTarget(out MemoryFragment f) || f == null)
//						gf.lane.ResetOne(gf.cycle); // Force the lane to reset one allocation
//					else disposedButNotNull++;

//				if (disposedButNotNull != 50)
//				{
//					Passed = false;
//					FailureMessage = $"{hw.GetType().Name}: should have collected 50 ghosts, got {disposedButNotNull} alive.";
//					return;
//				}

//				// Assert that the all ghost fragments are gone
//				if (hw.GetTotalActiveFragments() != 0)
//				{
//					Passed = false;
//					FailureMessage = $"{hw.GetType().Name}: should have reset the total allocation to 0 after forced resets.";
//					return;
//				}
//			}

//			if (opt.Contains("mh"))
//				using (var hw = new HeapHighway(ms, 1024))
//					allocAndManualReset(hw);

//			if (opt.Contains("nh"))
//				using (var hw = new MarshalHighway(ms, 1024))
//					allocAndManualReset(hw);

//			if (opt.Contains("mmf"))
//				using (var hw = new MappedHighway(ms, 1024))
//					allocAndManualReset(hw);

//			if (!Passed.HasValue) Passed = true;
//			IsComplete = true;
//		}
//	}
//}
