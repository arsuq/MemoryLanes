/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace System.Collections.Concurrent
{
	readonly ref struct TesseractPos
	{
		public TesseractPos(in int index)
		{
			D0 = index >> CUBE_SHIFT;
			var r = index & CUBE_REM;
			D1 = r >> PLANE_SHIFT;
			r = r & PLANE_REM;
			D2 = r >> BASE_SHIFT;
			D3 = r & BASE_REM;
		}

		public readonly int D0;
		public readonly int D1;
		public readonly int D2;
		public readonly int D3;

		public const int BASE_SHIFT = 8;
		public const int PLANE_SHIFT = 16;
		public const int CUBE_SHIFT = 24;

		public const int BASE_REM = 255;
		public const int PLANE_REM = (1 << 16) - 1;
		public const int CUBE_REM = (1 << 24) - 1;

		public const int PLANE = 1 << 16;
		public const int CUBE = 1 << 24;
	}
}
