using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using TestRunner;

namespace Tests.Surface
{
	using ArgMap = IDictionary<string, List<string>>;

	struct NoRefsStruct
	{
		public int Int;
		public double Dbl;
	}

	public class ParkingUnmanagedStructs : ITestSurface
	{
		public string Info => @"Tests allocation of structs on the unmanaged heap.";

		public bool RequireArgs => false;
		public string FailureMessage => string.Empty;
		public bool? Passed => passed;

		public unsafe Task Run(ArgMap args)
		{
			return Task.Run(() =>
			{
				passed = true;

				var p = stackalloc NoRefsStruct[1];
				var str = p[0];

				str.Int = int1;
				str.Dbl = dbl1;

				var mps = MarshalParkingSlot.Store(str);
				Task.Run(() => useParkingSlot(mps));

				var p2 = MarshalParkingSlot.Reserve<NoRefsStruct>(out MarshalParkingSlot mps2);
				p2->Int = int2;
				p2->Dbl = dbl2;

				Task.Run(() => useParkingSlot(mps2));

				var mpsLarge = new MarshalParkingSlot(20000000);
				mpsLarge.Span().Fill(7);
			});
		}

		void useParkingSlot(MarshalParkingSlot mps)
		{
			var str = mps.Load<NoRefsStruct>();

			Print.AsInfo("Int: {0}, Dbl: {1}", str.Int, str.Dbl);

			if ((str.Int != int1 && str.Int != int2) ||
				(str.Dbl != dbl1 && str.Dbl != dbl2)) passed = false;
		}

		int int1 = 323;
		int int2 = 423323;
		double dbl1 = 34562.4345;
		double dbl2 = 234123423.2342345;
		bool? passed;
	}
}
