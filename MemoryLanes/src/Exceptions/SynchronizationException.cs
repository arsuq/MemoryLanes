/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace System
{
	public class SynchronizationException : SystemException
	{
		public enum Code
		{
			NotSet = 0,
			LockAcquisition = 1,
			SignalAwaitTimeout = 2,
		}

		public SynchronizationException() { }
		public SynchronizationException(Code code) => ErrorCode = code;
		public SynchronizationException(Code code, string message) : base(message) => ErrorCode = code;
		public SynchronizationException(string message) : base(message) { }

		public readonly Code ErrorCode;
	}
}
