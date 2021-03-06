/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using TestSurface;

namespace Tests.Surface
{
	using ArgMap = IDictionary<string, List<string>>;

	struct NoRefsStruct
	{
		public int Int;
		public double Dbl;
	}

	public class AllocUnmanagedStructs : ITestSurface
	{
		public string Info => "Tests allocation of structs on the unmanaged heap.";

		public string Tags => string.Empty;
		public bool IndependentLaunchOnly => false;
		public string FailureMessage => string.Empty;
		public bool? Passed { get; private set; }
		public bool IsComplete { get; private set; }

		public unsafe Task Start(ArgMap args)
		{
			return Task.Run(() =>
			{
				Passed = true;

				var p = stackalloc NoRefsStruct[1];
				var str = p[0];

				str.Int = int1;
				str.Dbl = dbl1;

				var mps = MarshalSlot.Store(str);
				Task.Run(() => useParkingSlot(mps));

				var p2 = MarshalSlot.Reserve<NoRefsStruct>(out MarshalSlot mps2);
				p2->Int = int2;
				p2->Dbl = dbl2;

				Task.Run(() => useParkingSlot(mps2));

				var mpsLarge = new MarshalSlot(20000000);
				mpsLarge.Span().Fill(7);
			});
		}

		void useParkingSlot(MarshalSlot mps)
		{
			var str = mps.Load<NoRefsStruct>();

			Print.AsInfo("Int: {0}, Dbl: {1}", str.Int, str.Dbl);

			if ((str.Int != int1 && str.Int != int2) ||
				(str.Dbl != dbl1 && str.Dbl != dbl2)) Passed = false;
		}

		int int1 = 323;
		int int2 = 423323;
		double dbl1 = 34562.4345;
		double dbl2 = 234123423.2342345;
	}
}
