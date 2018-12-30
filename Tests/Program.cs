using System;
using TestRunner;

namespace Tests
{
	class Program
	{
		static void Main(string[] args)
		{
			new Runner().Run(args);

			Print.AsSystemTrace("Done. Press <Enter> to close.");
			Console.ReadLine();
		}
	}
}
