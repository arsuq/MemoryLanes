using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.SocketLoad
{
	public class Server
	{
		public Server(int port)
		{
			AppDomain.CurrentDomain.ProcessExit += ProcessExit;
			server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			server.Bind(new IPEndPoint(IPAddress.Loopback, port));
		}

		public async Task ReceiveLoop()
		{
			Console.WriteLine("Started.");

			var data = new List<byte>();
			server.Listen(100);

			while (true)
			{
				var client = await server.AcceptAsync().ConfigureAwait(false);
				new Task(() =>
					Process(client, (b) => Print.AsSuccess(
					   "{0} bytes received from {1}.",
					   b.Length,
					   ((IPEndPoint)client.RemoteEndPoint).Port.ToString())).Wait()
				).Start();
			}
		}

		async Task Process(Socket client, Action<byte[]> onmessage, bool stop = false)
		{
			try
			{
				Console.WriteLine("New client");
				Console.WriteLine();
				Console.WriteLine();

				using (var ns = new NetworkStream(client))
				{
					var header = new byte[4];
					byte[] frame = null;
					var total = 0;
					var read = 0;
					var frameLen = 0;

					while (!stop)
					{
						total = 0;
						read = await ns.ReadAsync(header, 0, 4).ConfigureAwait(false);

						// The other side is gone.
						// As long as the sender is not disposed/closed the ReadAsync will wait for 
						if (read < 1)
						{
							Print.AsError("The client is gone.");
							break;
						}

						frameLen = BitConverter.ToInt32(header, 0);

						// Send "Bad header" to the remote party
						if (frameLen < 1)
						{
							Print.AsError("Bad header, thread {0}", Thread.CurrentThread.ManagedThreadId);
							break;
						}

						frame = new byte[frameLen];
						Print.AsInfo("Frame length:{0}", frameLen);

						while (total < frameLen && !stop)
						{
							read = await ns.ReadAsync(frame, total, frameLen - total).ConfigureAwait(false);
							total += read;

							Print.AsInnerInfo("    read {0} on thread {1}", read, Thread.CurrentThread.ManagedThreadId);

							if (total >= frameLen)
							{
								onmessage?.Invoke(frame);
								break;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		void ProcessExit(object sender, EventArgs e)
		{
			try { server?.Close(); }
			catch { }
		}

		Socket server;
	}
}
