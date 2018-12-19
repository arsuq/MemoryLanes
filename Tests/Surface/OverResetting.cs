using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TestRunner;

namespace Tests.Surface
{
	public class OverResetting : ITestSurface
	{
		public string Info => "Tests lane resetting scenarios.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool RequireArgs => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			var ms = new MemoryLaneSettings(1024);
		
		}
	}
}
