/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace System
{
	public class MemoryLaneException : Exception
	{
		public enum Code
		{
			NotSet = 0,
			NotInitialized,
			InitFailure,
			MissingOrInvalidArgument,
			SizeOutOfRange,
			AllocFailure,
			NewLaneAllocFail,
			MaxLanesCountReached,
			MaxTotalAllocBytesReached,
			LaneNegativeReset,
			AttemptToAccessWrongLaneCycle,
			AttemptToAccessDisposedLane,
			AttemptToAccessClosedLane,
			IncorrectDisposalMode 
		}

		public MemoryLaneException() { }

		public MemoryLaneException(Code code, string msg = null) : base(msg) { ErrorCode = code; }

		public MemoryLaneException(string msg) : base(msg) { }

		public MemoryLaneException(string msg, Exception inner) : base(msg, inner) { }

		public readonly Code ErrorCode;
	}
}