using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface
{
	class FragmentStreamSurface : ITestSurface
	{
		public string Info => "Tests the FragmentStream class.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			try
			{
				if (args.ContainsKey("-all"))
					args.Add("-store", new List<string>() { "mh", "mmf", "nh" });

				args.AssertAll("-store");
				var opt = args["-store"];
				opt.AssertNothingOutsideThese("mh", "mmf", "nh");

				var iH = new Dictionary<string, IMemoryHighway>();

				iH.Add("mh", new HeapHighway(4000));
				iH.Add("nh", new MarshalHighway(4000));
				iH.Add("mmf", new MappedHighway(4000));

				var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

				foreach (var kv in iH)
				{
					if (!opt.Contains(kv.Key)) continue;
					var hwName = kv.Value.GetType().Name;

					using (var hw = kv.Value)
					using (var f = hw.AllocFragment(3000).CreateStream())
					{
						var dummy = new Dummy()
						{
							Int = 2,
							LS = new List<string>() { "A", "B", "C" },
							Map = new Dictionary<string, DateTime>()
							{
								{ "x", DateTime.Now },
								{ "y", DateTime.Now.AddSeconds(10) },
							}
						};

						dummy.SetInnerDummy(new InnerDummy(Guid.NewGuid(), 434.666M));

						var bf = new BinaryFormatter();

						bf.Serialize(f, dummy);
						f.Seek(0, SeekOrigin.Begin);

						var dummyr = (Dummy)bf.Deserialize(f);

						if (!Assert.SameValues(dummy, dummyr, flags))
						{
							Passed = false;
							FailureMessage = "The two dummies do not match";
							return;
						}

						$"BinaryFormatter serialization and deserialization works with {hwName}.".AsSuccess();
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

	[Serializable]
	public class Dummy
	{
		public void SetInnerDummy(InnerDummy id)
		{
			inner = id;
		}

		public int Int;
		public List<string> LS = new List<string>();
		public Dictionary<string, DateTime> Map = new Dictionary<string, DateTime>();

		protected InnerDummy inner;
	}

	[Serializable]
	public class InnerDummy
	{
		public InnerDummy(Guid g, decimal d)
		{
			GUID = g;
			privateDec = d;
		}

		public Guid GUID { get; set; }
		decimal privateDec;
	}
}