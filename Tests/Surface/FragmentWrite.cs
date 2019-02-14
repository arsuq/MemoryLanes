﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	class FragmentWrite : ITestSurface
	{
		public string Info => "Tests the MemoryFragment Write methods.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			if (args.ContainsKey("-all"))
				args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

			var opt = args["-store"];
			opt.AssertNothingOutsideThese("mh", "mmf", "nh");

			var stg = new MemoryLaneSettings(1024, 2);
			var iH = new Dictionary<string, IMemoryHighway>();

			var LEN = stg.DefaultCapacity;

			iH.Add("mh", new HeapHighway(stg, LEN, LEN));
			iH.Add("nh", new MarshalHighway(stg, LEN, LEN));
			iH.Add("mmf", new MappedHighway(stg, LEN, LEN));

			foreach (var kp in iH)
			{
				var hwName = kp.Value.GetType().Name;

				if (opt.Contains(kp.Key))
				{
					var hw = kp.Value;
					using (hw)
					{
						var f = hw.AllocFragment(200);
						var p = 0;

						bool b = true;
						int i = 10;
						double d = 2.2;
						DateTime dt = DateTime.Now;
						char c = 'c';
						Guid g = Guid.NewGuid();

						p = f.Write(b, p);
						p = f.Write(c, p);
						p = f.Write(dt, p);
						p = f.Write(d, p);
						p = f.Write(i, p);
						p = f.Write(g, p);

						p = 0;

						bool br = false;
						int ir = 0;
						double dr = 0;
						DateTime dtr = DateTime.MinValue;
						char cr = char.MinValue;
						Guid gr = Guid.Empty;

						p = f.Read(ref br, p);
						p = f.Read(ref cr, p);
						p = f.Read(ref dtr, p);
						p = f.Read(ref dr, p);
						p = f.Read(ref ir, p);
						p = f.Read(ref gr, p);

						if (br != b || cr != c || dtr != dt || dr != d || ir != i || gr != g)
						{
							Passed = false;
							FailureMessage = "The reads do not match the writes.";
							break;
						}
						else $"{hwName}: fragment reads and writes primitive types correctly.".AsSuccess();
					}
				}
			}

			if (!Passed.HasValue) Passed = true;
			IsComplete = true;
		}
	}
}