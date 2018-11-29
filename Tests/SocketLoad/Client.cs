using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tests.SocketLoad
{
	public class Client
	{
		public Client(int port)
		{
			AppDomain.CurrentDomain.ProcessExit += ProcessExit;
			client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 0));
			client.Connect(IPAddress.Loopback, port);
		}

		public async Task Send(int msgCount, int size, bool randomSize, int sleepms = 0)
		{
			var ns = client.GetStream();
			var rdm = new Random();

			for (; msgCount > 0; msgCount--)
			{
				var len = randomSize ? rdm.Next(1024, size) : size;
				var msg = new byte[len + 4];
				if (BitConverter.TryWriteBytes(msg, len))
				{
					if (sleepms > 0) await Task.Delay(sleepms);
					Console.WriteLine("Header bytes: {0}.{1}.{2}.{3}", msg[0], msg[1], msg[2], msg[3]);
					int check = BitConverter.ToInt32(msg, 0);
					Console.WriteLine("Header check: {0}", check);
					await ns.WriteAsync(msg);
				}
				else
				{
					Console.WriteLine("Can't set a header");
					break;
				}
			}
		}

		void ProcessExit(object sender, EventArgs e)
		{
			try { client?.Close(); }
			catch { }
		}

		TcpClient client;
	}
}
