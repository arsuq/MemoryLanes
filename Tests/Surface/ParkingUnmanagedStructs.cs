using System;
using System.Threading.Tasks;
using System.Collections.Generic;


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
		public string Info() => @"Tests allocation of structs on the unmanaged heap.";

		public unsafe void Run(ArgMap args)
		{
			var p = stackalloc NoRefsStruct[1];
			var str = p[0];

			str.Dbl = 231.3;
			str.Int = 12;

			var mps = MarshalParkingSlot.Store(str);
			Task.Run(() => useParkingSlot(mps));

			var p2 = MarshalParkingSlot.Reserve<NoRefsStruct>(out MarshalParkingSlot mps2);
			p2->Int = 1000;
			p2->Dbl = 2000.3331;

			Task.Run(() => useParkingSlot(mps2));

			var mpsLarge = new MarshalParkingSlot(20000000);
			mpsLarge.Span().Fill(7);
		}

		void useParkingSlot(MarshalParkingSlot mps)
		{
			var str = mps.Load<NoRefsStruct>();

			Print.AsInfo("Int: {0}, Dbl: {1}", str.Int, str.Dbl);
		}
	}
}
