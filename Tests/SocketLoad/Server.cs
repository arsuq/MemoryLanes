using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Tests.SocketLoad
{
	public class Server
	{
		public Server(int port)
		{
			AppDomain.CurrentDomain.ProcessExit += ProcessExit;
			server = new TcpListener(IPAddress.Loopback, port);
		}

		public async Task ReceiveLoop()
		{
			server.Start();
			Console.WriteLine("Started.");

			var data = new List<byte>();

			while (true)
			{
				var client = await server.AcceptTcpClientAsync().ConfigureAwait(false);
				var ns = client.GetStream();
				new Task(() =>
					Process(ns, (b) => Console.WriteLine(
					   "{0} bytes received from {1}.",
					   b.Length,
					   ((IPEndPoint)client.Client.RemoteEndPoint).Port.ToString())).Wait()
				).Start();
			}
		}

		async Task Process(NetworkStream ns, Action<byte[]> onmessage, bool stop = false)
		{
			try
			{
				var header = new byte[4];
				byte[] frame = null;
				var total = 0;
				var read = 0;
				var frameLen = 0;

				while (!stop && ns.DataAvailable)
				{
					total = 0;
					read = await ns.ReadAsync(header, 0, 4).ConfigureAwait(false);
					if (read < 1)
					{
						Console.WriteLine("!!! Read nothing {0} as header", read);
						continue;
					}
					frameLen = BitConverter.ToInt32(header);
					frame = new byte[frameLen];
					Console.WriteLine("Frame length:{0}", frameLen);

					while (ns.DataAvailable && read > 0)
					{
						read = await ns.ReadAsync(frame, total, frameLen - total).ConfigureAwait(false);
						total += read;

						if (total >= frameLen)
						{
							onmessage?.Invoke(frame);
							break;
						}
					}
				}
			}
			catch (Exception ex) { Console.WriteLine(ex.Message); }
		}

		void ProcessExit(object sender, EventArgs e)
		{
			try { server?.Stop(); }
			catch { }
		}

		TcpListener server;
	}
}
