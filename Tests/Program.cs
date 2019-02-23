/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestSurface;

namespace Tests
{
	class Program
	{
		static void Main(string[] args)
		{
			new Runner().Run(args);

			Print.AsSystemTrace("Done. Press <Enter> to close.");
			Console.ReadLine();
		}
	}

	class XX : ITestSurface
	{
		public string Info => "";

		public string FailureMessage => "";

		public bool? Passed => true;

		public bool IsComplete => true;

		public bool IndependentLaunchOnly => false;

		public async Task Run(IDictionary<string, List<string>> args)
		{

		}
	}
}
