using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TestSurface;

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
			Print.AsInfo("SocketLoad client Send()");
			Print.AsWarn("Do not close the client even if all data seems to be sent, this will tear down the connection");

			var ns = client.GetStream();
			var rdm = new Random();
			var sendBuff = new Memory<byte>(new byte[size + 4]);
			var header = sendBuff.Slice(0, 4);

			byte v = 0;
			for (int i = 4; i < sendBuff.Length; i++) sendBuff.Span[i] = ++v;

			for (; msgCount > 0; msgCount--)
			{
				var len = randomSize ? rdm.Next(1024, size) : size;
				if (BitConverter.TryWriteBytes(header.Span, len))
				{
					var hs = header.Span.ToArray();
					Print.AsInfo("Frame length: {0}", len);
					Print.AsInfo("Frame header bytes: {0}.{1}.{2}.{3}", hs[0], hs[1], hs[2], hs[3]);
					Print.AsInfo("Header value double check: {0} ", BitConverter.ToInt32(hs, 0));
					if (sleepms > 0) await Task.Delay(sleepms);
					await ns.WriteAsync(sendBuff.Slice(0, len + 4));
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
