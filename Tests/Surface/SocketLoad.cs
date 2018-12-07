using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestRunner;
using Tests.SocketLoad;

namespace Tests.Surface
{
	using ArgMap = IDictionary<string, List<string>>;

	public class SocketLoad : ITestSurface
	{
		public string Info() =>
			"Runs as either a server - accepting messages or a client - producing them. " + Environment.NewLine +
			"Args: -mode: <s> or <c>, if <c> -mc: message count, -ms: message size, -r: randomize size" + Environment.NewLine +
			"Example client: -mode c -mc 200 -ms 1024 -r" + Environment.NewLine +
			"Example server: -mode s";

		public bool RequireArgs => true;
		public string FailureMessage => string.Empty;
		public bool? Passed => passed;

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

		bool? passed;
	}
}
