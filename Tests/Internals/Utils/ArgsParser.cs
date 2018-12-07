using System;
using System.Collections.Generic;

namespace Tests.Internals.Utils
{
	public class ArgsParser
	{
		public const char SWITCH_SYMBOL = '-';

		public ArgsParser(string[] args)
		{
			if (args == null) throw new ArgumentNullException("args");

			if (args.Length > 0)
			{
				var sw = string.Empty;

				foreach (var arg in args)
				{
					if (arg[0] == SWITCH_SYMBOL)
					{
						sw = arg;
						argsMap.Add(arg, new List<string>());
					}
					else if (!string.IsNullOrEmpty(sw))
						argsMap[sw].Add(arg);
				}
			}
		}

		public IDictionary<string, List<string>> Map => argsMap;

		Dictionary<string, List<string>> argsMap = new Dictionary<string, List<string>>();
	}
}
