using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestRunner;

namespace Tests.SocketLoad
{
	public class Server
	{
		enum AllocType { Heap, MMF, Marshal }

		public Server(int port)
		{
			AppDomain.CurrentDomain.ProcessExit += ProcessExit;
			server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			server.Bind(new IPEndPoint(IPAddress.Loopback, port));
		}

		public async Task ReceiveLoop()
		{
			Print.AsInfo("SocketLoad server started.");

			var data = new List<byte>();
			server.Listen(100);

			var end = Task.Run(() => Console.ReadLine());

			Print.AsWarn("The server will listen indefinitely, press <Enter> to stop accepting clients and quit.");

			while (true)
			{
				var accept = server.AcceptAsync();
				accept.ConfigureAwait(false);
				if (Task.WaitAny(end, accept) == 1)
				{
					var client = accept.Result;
					new Task(() =>
						Process(AllocType.Heap, client, (len) => Print.AsSuccess(
						   "{0} bytes received from {1}.",
						   len,
						   ((IPEndPoint)client.RemoteEndPoint).Port.ToString())).Wait()
					).Start();
				}
				else break;
			}
		}

		async Task Process(AllocType at, Socket client, Action<int> onmessage, bool stop = false)
		{
			Print.AsInfo(at.ToString() + Environment.NewLine);

			try
			{
				Print.AsInfo("New client" + Environment.NewLine);

				IHighwayAlloc hw = null;
				var lanes = new int[] { 1025, 2048 };

				switch (at)
				{
					case AllocType.Heap:
					hw = new HeapHighway(lanes);
					break;
					case AllocType.MMF:
					hw = new MappedHighway(lanes);
					break;
					case AllocType.Marshal:
					hw = new MarshalHighway(lanes);
					break;
					default:
					throw new ArgumentNullException();
				}

				using (hw)
				using (var ns = new NetworkStream(client))
				{
					var header = new byte[4];
					var spoon = new byte[16000];
					var total = 0;
					var read = 0;
					var frameLen = 0;

					while (!stop)
					{
						total = 0;
						read = await ns.ReadAsync(header, 0, 4).ConfigureAwait(false);
						Print.AsInfo("Received header bytes: {0}.{1}.{2}.{3}", header[0], header[1], header[2], header[3]);

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

						using (var frag = hw.AllocFragment(frameLen))
						{
							Print.AsInfo("Frame length:{0}", frameLen);

							// The sip length guards against jumping into the next frame
							var sip = 0;
							while (total < frameLen && !stop)
							{
								sip = frameLen - total;
								if (sip > spoon.Length) sip = spoon.Length;

								// the read amount could be smaller than the sip
								read = await ns.ReadAsync(spoon, 0, sip).ConfigureAwait(false);
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
			catch (MemoryLaneException mex)
			{
				Print.AsError(mex.Message);
				Print.AsError(mex.ErrorCode.ToString());
				Print.AsError(mex.StackTrace);
			}
			catch (Exception ex)
			{
				Print.AsError(ex.Message);
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
