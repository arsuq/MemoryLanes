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
	public class AllocDisposeLatency : ITestSurface
	{
		public string Info =>
			"Compares lanes count, alloc and dispose delays" +
			" with different TRIES/AWAIT values.";

		public string Tags => string.Empty;
		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => true;
		public bool IsComplete { get; private set; }

		public async Task Start(IDictionary<string, List<string>> args)
		{
			await Task.Delay(0);

			// [i] Try changing TRIES and AWAIT and
			// observe lane count and delay numbers.
			// (*) TRIES = 1. AWAIT = 0 is the fastest possible allocation.

			const int totalAllocs = 1000_000;
			const int TRIES = 4;
			const int AWAIT = 5;
			var cde = new CountdownEvent(totalAllocs);

			using (var hw = new HeapHighway(1000_000))
			{
				var start = DateTime.Now;

				for (int i = 0; i < totalAllocs; i++)
					ThreadPool.QueueUserWorkItem((x) =>
					{
						var f = hw.AllocFragment(1, TRIES, AWAIT);

						// Dispose possibly on another thread.
						ThreadPool.QueueUserWorkItem((frag) =>
						{
							frag.Dispose();
							cde.Signal();
						}, f, false);
					});

				cde.Wait();
				var ms = DateTime.Now.Subtract(start).TotalMilliseconds;
				var lc = hw.GetLanesCount();
				var nd = hw.GetTotalActiveFragments();

				if (nd != 0)
				{
					FailureMessage = $"There are {nd} non disposed frags!";
					Passed = false;
					return;
				}

				$"OK: {totalAllocs} frags were created and disposed for {ms} MS in {lc} lanes."
					.AsSuccess();
			}

			Passed = true;
			IsComplete = true;
		}
	}
}