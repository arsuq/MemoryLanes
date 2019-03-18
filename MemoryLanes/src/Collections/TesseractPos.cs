/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.InteropServices;

namespace System.Collections.Concurrent
{
	struct TesseractPos
	{
		public TesseractPos(int index)
		{
			// Skip zero init and copy the set
			unsafe
			{
				byte* p = (byte*)&index;

				D0 = p[3];
				D1 = p[2];
				D2 = p[1];
				D3 = p[0];
			}
		}

		public void Set(int index)
		{
			unsafe
			{
				byte* p = (byte*)&index;

				D0 = p[3];
				D1 = p[2];
				D2 = p[1];
				D3 = p[0];
			}
		}

		public int D0;
		public int D1;
		public int D2;
		public int D3;
	}
}
