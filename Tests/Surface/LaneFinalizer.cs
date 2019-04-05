/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	public class LaneFinalizer : ITestSurface
	{
		public string Info => "Tests whether undisposed lanes would release the underlying unmanaged resources.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			await Task.Delay(0);

			var ms = new HighwaySettings(1024);
			
			// so that no refs are held 
			ms.RegisterForProcessExitCleanup = false;
			// The unmanaged resource
			var MMF_FileID = string.Empty; 

			// Must be in a separate non-async function so that no refs are holding it.
			void fin()
			{
				// Create without 'using' and forget to dispose
				var mmh = new MappedHighway(ms, 1024);
				MMF_FileID = mmh[0].FileID;
				mmh.Alloc(100);
			}

			fin();
			GC.Collect(2);
			Thread.Sleep(5000);  

			if (!File.Exists(MMF_FileID))
			{
				Print.AsSuccess($"MappedHighway: the underlying file {MMF_FileID} was cleaned up by the finalizer.");
				Passed = true;
			}
			else
			{
				Print.AsTestFailure("MappedHighway: the finalizer was not triggered or the file deletion failed.");
				Passed = false;
			}

			IsComplete = true;
		}
	}
}
