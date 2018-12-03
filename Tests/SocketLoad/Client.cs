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
			var sendBuff = new byte[size + 4];

			byte v = 0;
			for (int i = 4; i < sendBuff.Length; i++) sendBuff[i] = ++v;

			for (; msgCount > 0; msgCount--)
			{
				var len = randomSize ? rdm.Next(1024, size) : size;
				if (BitConverter.TryWriteBytes(sendBuff, len))
				{
					if (sleepms > 0) await Task.Delay(sleepms);
					await ns.WriteAsync(sendBuff);
				}
				else
				{
					Print.AsError("Can't set a header");
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
