/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace System.Collections.Concurrent
{
	ref struct CCubePos
	{
		public CCubePos(in int index)
		{
			if (index < PLANE)
			{
				D0 = 0;
				D1 = index >> BASE_SHIFT; // index / BASE
				D2 = index & BASE_REM;    // index % BASE
			}
			else
			{
				D0 = index >> PLANE_SHIFT;
				var r = index & PLANE_REM;
				D1 = r >> BASE_SHIFT;
				D2 = r & BASE_REM;
			}
		}

		public readonly int D0;
		public readonly int D1;
		public readonly int D2;

		public const int BASE_SHIFT = 10;
		public const int PLANE_SHIFT = 20;
		public const int BASE_REM = 1023;
		public const int PLANE_REM = (1 << 20) - 1;
		public const int PLANE = 1 << 20;
	}
}
