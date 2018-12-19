using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestRunner;

namespace Tests.Surface
{
	public class OverResetting : ITestSurface
	{
		public string Info => "Tests manual lane over resetting.";

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

			var ms = new MemoryLaneSettings(1024, 1, 1024);

			// Allocates 100 fragments and continuously resets one, while 
			// new fragments are allocated. 
			void allocAndManualReset(IMemoryHighway hw)
			{
				var F = new List<MemoryFragment>();

				for (int i = 0; i < 100; i++)
					F.Add(hw.AllocFragment(4));
			}

			if (opt.Contains("mh"))
				using (var hw = new HeapHighway(ms, 1024))
					allocAndManualReset(hw);

			if (opt.Contains("nh"))
				using (var hw = new MarshalHighway(ms, 1024))
					allocAndManualReset(hw);

			if (opt.Contains("mmf"))
				using (var hw = new MappedHighway(ms, 1024))
					allocAndManualReset(hw);

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}
