/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestSurface;
using Tests.SocketLoad;

namespace Tests.Surface
{
	using ArgMap = IDictionary<string, List<string>>;

	public class SocketLoad : ITestSurface
	{
		public string Info =>
			"Runs as either a server - accepting messages or a client - producing them. " + Environment.NewLine +
			"Args: -mode: <s> or <c>, if <c> -mc: message count, -ms: message size, -r: randomize size" + Environment.NewLine +
			"Example client: -mode c -mc 200 -ms 1024 -r" + Environment.NewLine +
			"Example server: -mode s";

		public bool RequiresArgs => true;
		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IsComplete { get; private set; }

		public Task Run(ArgMap args)
		{
			if (args == null || args.Count < 1) throw new ArgumentException("args");

			var mode = args["-mode"][0];

			if (mode == "s") return new Server(33444).ReceiveLoop();
			else if (mode == "c")
			{
				int.TryParse(args["-mc"][0], out int mc);
				int.TryParse(args["-ms"][0], out int size);
				var rand = args.ContainsKey("-r");
				return new Client(33444).Send(mc, size, rand);
			}
			else throw new ArgumentException("The SocketLoad -mode can only be <s> or <c>");
		}
	}
}
