using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	class FormatLane : ITestSurface
	{
		public string Info => "Tests the lane.Format(stream) method";

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

			var iH = new Dictionary<string, IMemoryHighway>();

			iH.Add("mh", new HeapHighway(100));
			iH.Add("nh", new MarshalHighway(100));
			iH.Add("mmf", new MappedHighway(100));

			var F = new byte[2][] {
				new byte[20],
				new byte[200]
			};

			foreach (var kp in iH)
			{
				var hwName = kp.Value.GetType().Name;

				if (opt.Contains(kp.Key))
				{
					using (var hw = kp.Value)
						foreach (var src in F)
							using (var ms = new MemoryStream(src))
							{
								var cap = hw[0].LaneCapacity;
								var read = hw[0].Format(ms, cap);

								ms.Seek(0, SeekOrigin.Begin);

								using (var f = hw[0].Alloc(cap))
								using (var fs = f.CreateStream())
								{
									for (int i = 0; i < read; i++)
										if (fs.ReadByte() != ms.ReadByte())
										{
											Passed = false;
											FailureMessage = $"Reading different values from formatted lane and its source. The cap is {cap}";
											return;
										}
								}

								$"{hwName}: OK for source {src.Length}b, cap {cap}b".AsInfo();
							}

					$"Lane formatting seems to work on a {hwName} lane".AsSuccess();
				}
			}

			Passed = true;
			IsComplete = true;
		}
	}
}