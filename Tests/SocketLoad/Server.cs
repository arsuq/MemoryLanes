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

			var end = Task.Run(() => Console.ReadLine());

			while (true)
			{
				var accept = server.AcceptAsync();
				accept.ConfigureAwait(false);
				if (Task.WaitAny(end, accept) == 1)
				{
					var client = accept.Result;
					new Task(() =>
						ProcessMMF(client, (len) => Print.AsSuccess(
						   "{0} bytes received from {1}.",
						   len,
						   ((IPEndPoint)client.RemoteEndPoint).Port.ToString())).Wait()
					).Start();
				}
				else break;
			}
		}

		async Task Process(Socket client, Action<int> onmessage, bool stop = false)
		{
			try
			{
				Print.AsInfo("New client" + Environment.NewLine);

				var hw = new HeapHighway(1025, 2040);

				using (var ns = new NetworkStream(client))
				{
					var header = new byte[4];
					var total = 0;
					var read = 0;
					var frameLen = 0;

					while (!stop)
					{
						total = 0;
						read = await ns.ReadAsync(header, 0, 4).ConfigureAwait(false);

						// The other side is gone.
						// As long as the sender is not disposed/closed the ReadAsync will wait  
						if (read < 1)
						{
							Print.AsError("The client is gone.");
							break;
						}

						frameLen = BitConverter.ToInt32(header, 0);

						if (frameLen < 1)
						{
							Print.AsError("Bad header, thread {0}", Thread.CurrentThread.ManagedThreadId);
							break;
						}

						using (var frag = hw.Alloc(frameLen))
						{
							Print.AsInfo("Frame length:{0}", frameLen);

							while (total < frameLen && !stop)
							{
								read = await ns.ReadAsync(frag.Memory.Slice(total)).ConfigureAwait(false);
								total += read;

								Print.AsInnerInfo("    read {0} on thread {1}", read, Thread.CurrentThread.ManagedThreadId);

								if (total >= frameLen)
								{
									onmessage?.Invoke(frag.Memory.Length);
									break;
								}
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

		async Task ProcessMMF(Socket client, Action<int> onmessage, bool stop = false)
		{
			Print.AsInfo("MappedHighway" + Environment.NewLine);

			try
			{
				Print.AsInfo("New client" + Environment.NewLine);

				using (var mh = new MappedHighway(1025, 2040))
				using (var ns = new NetworkStream(client))
				{
					var header = new byte[4];
					var spoon = new byte[20000];
					var total = 0;
					var read = 0;
					var frameLen = 0;

					while (!stop)
					{
						total = 0;
						read = await ns.ReadAsync(header, 0, 4).ConfigureAwait(false);

						// The other side is gone.
						// As long as the sender is not disposed/closed the ReadAsync will wait  
						if (read < 1)
						{
							Print.AsError("The client is gone.");
							break;
						}

						frameLen = BitConverter.ToInt32(header, 0);

						if (frameLen < 1)
						{
							Print.AsError("Bad header, thread {0}", Thread.CurrentThread.ManagedThreadId);
							break;
						}

						using (var frag = mh.Alloc(frameLen))
						{
							Print.AsInfo("Frame length:{0}", frameLen);

							while (total < frameLen && !stop)
							{
								read = await ns.ReadAsync(spoon, 0, spoon.Length).ConfigureAwait(false);
								frag.Write(spoon, total, read);
								total += read;

								Print.AsInnerInfo("    read {0} on thread {1}", read, Thread.CurrentThread.ManagedThreadId);

								if (total >= frameLen)
								{
									onmessage?.Invoke(total);
									break;
								}
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
