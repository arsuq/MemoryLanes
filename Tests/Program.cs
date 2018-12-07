using System;
using Tests.SocketLoad;

namespace Tests
{
	class Program
	{
		static void Main(string[] args)
		{
			new Runner().Run(args);

			Console.WriteLine("Done. Press <Enter> to close.");
			Console.ReadLine();
		}
	}
}
