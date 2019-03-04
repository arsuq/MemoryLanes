/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface.Stream
{
	class StreamExtSurface : ITestSurface
	{
		public string Info => "Tests the StreamExt extensions.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IndependentLaunchOnly => false;
		public bool IsComplete { get; private set; }

		public async Task Run(IDictionary<string, List<string>> args)
		{
			var D = new byte[2][] {
				new byte[100],
				new byte[300]
			};

			foreach (var destb in D)
			{
				var srcb = new byte[200];
				var buff = new byte[4];
				byte b = 0;

				for (int i = 0; i < srcb.Length; i++)
					srcb[i] = b++;

				var s = new MemoryStream(srcb);
				var t = new MemoryStream(destb);
				var c = (int)(t.Length - t.Position);

				var read = await t.ReadFrom(s, c, buff);

				s.Seek(0, SeekOrigin.Begin);
				t.Seek(0, SeekOrigin.Begin);

				for (int i = 0; i < read; i++)
					if (s.ReadByte() != t.ReadByte())
					{
						Passed = false;
						FailureMessage = $"Source {srcb.Length} and target {destb.Length} differ";
						return;
					}

				$"Successfully copied {read} bytes from {srcb.Length}b source to {destb.Length}b target.".AsSuccess();
			}

			Passed = true;
			IsComplete = true;
		}
	}
}
