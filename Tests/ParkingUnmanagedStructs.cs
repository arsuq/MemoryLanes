using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
	struct NoRefsStruct
	{
		public int Int;
		public double Dbl;
	}

	public static class ParkingUnmanagedStructs
	{
		public unsafe static void Run()
		{
			var p = stackalloc NoRefsStruct[1];
			var str = p[0];

			str.Dbl = 231.3;
			str.Int = 12;

			var mps = MarshalParkingSlot.Store<NoRefsStruct>(str);
			Task.Run(() => useParkingSlot(mps));

			var p2 = MarshalParkingSlot.Reserve<NoRefsStruct>(out MarshalParkingSlot mps2);
			p2->Int = 1000;
			p2->Dbl = 2000.3331;

			Task.Run(() => useParkingSlot(mps2));
		}

		static void useParkingSlot(MarshalParkingSlot mps)
		{
			var str = mps.Load<NoRefsStruct>();

			Console.WriteLine("Int: {0}, Dbl: {1}", str.Int, str.Dbl);
		}
	}
}
