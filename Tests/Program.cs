using System;
using Tests.SocketLoad;

namespace Tests
{
	class Program
	{
		static void Main(string[] args)
		{
			//SocketLoad(args);
			ParkingUnmanagedStructs.Run();

			Console.WriteLine("Done. Press <Enter> to close.");
			Console.ReadLine();
		}

		static void SocketLoad(string[] args)
		{
			if (args == null || args.Length < 1)
			{
				Console.WriteLine("Args: mode, number of messages, message count, random size ");
				Console.WriteLine(" -s: server mode, -c: client mode, -r random size ");
				Console.WriteLine("Example: -c 200 1024 -r");
				return;
			}

			try
			{
				var c = args[0];

				if (c == "-s") new Server(33444).ReceiveLoop().Wait();
				else if (c == "-c")
				{
					var mc = 0;
					var size = 0;
					int.TryParse(args[1], out mc);
					int.TryParse(args[2], out size);
					var rand = args.Length > 3 && args[3] == "-r";
					new Client(33444).Send(mc, size, rand).Wait();
				}
			}
			catch (Exception ex) { Console.WriteLine(ex.Message); }
		}
	}
}
