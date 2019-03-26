/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestSurface;

namespace Tests.Surface.Collections
{
	public class TesseractMapSurface : ITestSurface
	{
		public string Info => "Tests either the TesseractMap<K,V> class.";

		public string FailureMessage { get; private set; }
		public bool? Passed { get; private set; }
		public bool IsComplete { get; private set; }
		public bool IndependentLaunchOnly => false;

		public async Task Run(IDictionary<string, List<string>> args)
		{
			try
			{
				//if (!set()) return;
				//if (!concurrentRW()) return;

				var t = setLatency();
				getLatency(t.qb, t.cd);

				Passed = true;
				IsComplete = true;
			}
			catch (Exception ex)
			{
				Passed = false;
				FailureMessage = ex.Message;
			}
		}

		bool set()
		{
			const int PRIME = 7;
			var rdm = new Random();
			var map = new Tesseract<int, int>(PRIME, -1);

			for (int l = 0; l < 2; l++)
			{
				for (int i = 0; i < 100; i++)
					map.Set(i, i);

				for (int i = 0; i < 100; i++)
				{
					if (map[i] == -1)
					{
						Passed = false;
						FailureMessage = "Missing values.";
						return false;
					}

					if (map[i] != i)
					{
						Passed = false;
						FailureMessage = "The get is wrong.";
						return false;
					}
				}

				if (l < 1) map.Resize(0, false);
			}

			"Basic get, set passed.".AsSuccess();

			var smap = new Tesseract<string, int>(PRIME, -1);

			for (int l = 0; l < 2; l++)
			{
				for (int i = 0; i < 100; i++)
					smap.Set(i.ToString(), i);

				for (int i = 0; i < 100; i++)
				{
					var s = i.ToString();

					if (smap[s] == -1)
					{
						Passed = false;
						FailureMessage = "Strings: Missing values.";
						return false;
					}

					if (smap[s] != i)
					{
						Passed = false;
						FailureMessage = "Strings: The get is wrong.";
						return false;
					}
				}

				if (l < 1) smap.Resize(0, false);
			}

			"Basic get, set with string keys passed.".AsSuccess();

			return true;
		}

		bool concurrentRW()
		{
			var qb = new Tesseract<string, int>(17, -1);
			var R = new Task[200];
			var W = new Task[100];
			var S = new string[10000];
			var M = 17;
			var stop = 0;

			for (int i = 0; i < S.Length; i++)
				S[i] = i.ToString();

			async Task read(int idx, int delay)
			{
				try
				{
					for (int i = 0; i < 100 && stop < 1; i++)
					{
						var key = S[idx];
						var value = qb[key];

						if (value != -1 && value != idx * M)
						{
							Interlocked.Exchange(ref stop, 1);
							Passed = false;
							FailureMessage = $"ConcurrentRW error: key: {key} value: {value}";
						}
						await Task.Delay(delay);
					}
				}
				catch (Exception ex)
				{
					Interlocked.Exchange(ref stop, 1);
					Passed = false;
					FailureMessage = ex.Message;
				}
			}

			async Task write(int idx, int delay)
			{
				try
				{
					for (int i = 0; i < 100 && stop < 1; i++)
					{
						var key = S[idx];
						var value = idx * M;
						qb.Set(key, value);
						await Task.Delay(delay);
					}
				}
				catch (Exception ex)
				{
					Interlocked.Exchange(ref stop, 1);
					Passed = false;
					FailureMessage = ex.Message;
				}
			}

			var rdm = new Random();

			for (int i = 0; i < R.Length; i++)
			{
				var ms = rdm.Next(0, 70);
				R[i] = read(i, ms);
			}

			for (int i = 0; i < W.Length; i++)
			{
				var ms = rdm.Next(0, 70);
				W[i] = write(i, ms);
			}

			Task.WaitAll(R);
			Task.WaitAll(W);

			return true;
		}

		(Tesseract<string, string> qb, ConcurrentDictionary<string, string> cd) setLatency()
		{
			const int COUNT = 2 << 21;
			int stop = 0;
			DateTime startTime;
			TimeSpan qbTime, dictTime;

			var qb = new Tesseract<string, string>(TesseractPrime.P196613, null, 4);
			var cd = new ConcurrentDictionary<string, string>();

			startTime = DateTime.Now;

			Parallel.For(0, COUNT, new ParallelOptions() { MaxDegreeOfParallelism = 200 }, (i) =>
			{
				if (stop > 0) return;

				try
				{
					var key = Guid.NewGuid().ToString();
					qb.Set(key, key);
				}
				catch (Exception ex)
				{
					Interlocked.Exchange(ref stop, 1);
					ex.Message.AsError();
				}
			});

			qbTime = DateTime.Now.Subtract(startTime);
			startTime = DateTime.Now;

			Parallel.For(0, COUNT, new ParallelOptions() { MaxDegreeOfParallelism = 200 }, (i) =>
			{
				if (stop > 0) return;

				try
				{
					var key = Guid.NewGuid().ToString();
					cd.TryAdd(key, key);
				}
				catch (Exception ex)
				{
					Interlocked.Exchange(ref stop, 1);
					ex.Message.AsError();
				}
			});

			dictTime = DateTime.Now.Subtract(startTime);

			var p = $"Set latency for {COUNT} GUID strings: Tesseract [{qbTime.Seconds}s {qbTime.Milliseconds}ms] " +
			$"ConcurrentDict [{dictTime.Seconds}s {dictTime.Milliseconds}ms]";
			p.AsWarn();

			return (qb, cd);
		}

		void getLatency(Tesseract<string, string> qb, ConcurrentDictionary<string, string> cd)
		{
			const int COUNT = 2 << 21;
			int stop = 0;
			DateTime startTime;
			TimeSpan qbTime, dictTime;

			var QB_KEYS = new List<string>(qb.Keys());
			var CD_KEYS = new List<string>(cd.Keys);

			startTime = DateTime.Now;

			Parallel.For(0, COUNT, new ParallelOptions() { MaxDegreeOfParallelism = 200 }, (i) =>
			{
				if (stop > 0) return;

				try
				{
					var key = QB_KEYS[i % QB_KEYS.Count];
					var v = qb.Get(key);
				}
				catch (Exception ex)
				{
					Interlocked.Exchange(ref stop, 1);
					ex.Message.AsError();
				}
			});

			qbTime = DateTime.Now.Subtract(startTime);
			startTime = DateTime.Now;

			Parallel.For(0, COUNT, new ParallelOptions() { MaxDegreeOfParallelism = 200 }, (i) =>
			{
				if (stop > 0) return;

				try
				{
					var key = CD_KEYS[i % CD_KEYS.Count];
					var vb = cd[key];
				}
				catch (Exception ex)
				{
					Interlocked.Exchange(ref stop, 1);
					ex.Message.AsError();
				}
			});

			dictTime = DateTime.Now.Subtract(startTime);

			var p = $"Get latency for {COUNT} GUID strings: Tesseract [{qbTime.Seconds}s {qbTime.Milliseconds}ms] " +
			$"ConcurrentDict [{dictTime.Seconds}s {dictTime.Milliseconds}ms]";
			p.AsWarn();
		}
	}
}
