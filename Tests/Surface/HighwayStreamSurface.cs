using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	public class HighwayStreamSurface : ITestSurface
	{
		public string Info => "Tests the HighwayStream class. Args: -store mh mmf nh";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			try
			{
				if (args.ContainsKey("+all"))
					args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

				args.AssertAll("-store");
				var opt = args["-store"];
				opt.AssertNothingOutsideThese("mh", "mmf", "nh");

				var iH = new Dictionary<string, IMemoryHighway>();

				iH.Add("mh", new HeapHighway(1024));
				iH.Add("nh", new MarshalHighway(1024));
				iH.Add("mmf", new MappedHighway(1024));

				var xsbuff = new byte[3];
				var sbuff = new byte[8];
				var lbuff = new byte[20];
				var bytes = new byte[17];
				for (byte i = 0; i < bytes.Length; i++)
					bytes[i] = i;

				foreach (var kv in iH)
				{
					if (!opt.Contains(kv.Key)) continue;
					var hwName = kv.Value.GetType().Name;

					using (var hw = kv.Value)
					using (var hs = hw.CreateStream(5))
					{
						// Will allocate a few fragments
						hs.Write(bytes);

						// Read from different starting positions
						foreach (int offset in new int[] { 0, 1, 2, 3, 4, 5, 6 })
						{
							hs.Seek(offset, SeekOrigin.Begin);

							if (hs.Position != offset)
							{
								Passed = false;
								FailureMessage = $"Seeking position {offset} with SeekOrigin.Begin failed on {hwName}.";
								return;
							}

							hs.Read(sbuff, 0, sbuff.Length);

							for (int i = 0; i < sbuff.Length; i++)
								if (bytes[i + offset] != sbuff[i])
								{
									Passed = false;
									FailureMessage = $"Reading wrong values with small buffer on {hwName}.";
									return;
								}

							hs.Seek(offset, SeekOrigin.Begin);
							hs.Read(lbuff, 0, lbuff.Length);

							for (int i = 0; i < bytes.Length - offset; i++)
								if (bytes[i + offset] != lbuff[i])
								{
									Passed = false;
									FailureMessage = $"Reading wrong values with large buffer on {hwName}, offset {offset}.";
									return;
								}

							hs.Seek(-hs.Length + offset, SeekOrigin.End);

							if (hs.Position != offset)
							{
								Passed = false;
								FailureMessage = $"Seeking position {offset} with SeekOrigin.End failed on {hwName}";
								return;
							}

							hs.Read(xsbuff, 0, xsbuff.Length);

							for (int i = 0; i < xsbuff.Length; i++)
								if (bytes[i + offset] != xsbuff[i])
								{
									Passed = false;
									FailureMessage = $"Reading wrong values with xs buffer on {hwName}, offset {offset}.";
									return;
								}
						}

						$"Read/Write works on {hwName}".AsSuccess();
					}
				}

				Passed = true;
				IsComplete = true;
			}
			catch (Exception ex)
			{
				Passed = false;
				FailureMessage = ex.Message;
			}
		}
	}
}

