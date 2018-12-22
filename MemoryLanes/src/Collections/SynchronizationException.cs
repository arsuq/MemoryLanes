namespace System
{
	public class SynchronizationException : SystemException
	{
		public enum Code
		{
			NotSet = 0,
			LockAcquisition = 1
		}

		public SynchronizationException() { }
		public SynchronizationException(Code code) => ErrorCode = code;
		public SynchronizationException(Code code, string message) : base(message) => ErrorCode = code;
		public SynchronizationException(string message) : base(message) { }

		public readonly Code ErrorCode;
	}
}
