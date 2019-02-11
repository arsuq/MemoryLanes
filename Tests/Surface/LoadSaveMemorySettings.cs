using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tests.Internals;
using TestSurface;

namespace Tests.Surface
{
	class LoadSaveMemorySettings : ITestSurface
	{
		public string Info => "Tests saving to file and loading memory lane settings.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			try
			{
				const string FILENAME = "MemoryLaneSettings.xml";

				if (File.Exists(FILENAME)) File.Delete(FILENAME);

				var ms = new MemoryLaneSettings(2345, 32, MemoryLane.DisposalMode.IDispose);

				Serializer.ToXmlFile(ms, FILENAME);
				var msl = Serializer.FromXmlFile<MemoryLaneSettings>(FILENAME);

				if (ms.DefaultCapacity != msl.DefaultCapacity ||
					ms.Disposal != msl.Disposal ||
					ms.MaxLanesCount != msl.MaxLanesCount ||
					ms.MaxTotalAllocatedBytes != msl.MaxTotalAllocatedBytes)
				{
					Passed = false;
					FailureMessage = "The settings are different.";
				}
				else Passed = true;
			}
			catch (Exception ex)
			{
				Passed = false;
				FailureMessage = ex.Message;
			}
		}
	}
}
