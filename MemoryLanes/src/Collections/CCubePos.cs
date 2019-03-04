namespace System.Collections.Concurrent
{
	ref struct CCubePos
	{
		public CCubePos(int index)
		{
			if (index < PLANE)
			{
				D0 = 0;
				D1 = index / SIDE;
				D2 = index % SIDE;
			}
			else
			{
				D0 = index / PLANE;
				var r = index % PLANE;
				D1 = r / SIDE;
				D2 = r % SIDE;
			}
		}

		public readonly int D0;
		public readonly int D1;
		public readonly int D2;

		// Such that SIDE^3 > int.MaxValue
		public const int SIDE = 1291;
		public const int PLANE = 1291 * 1291;
	}
}
