using System;

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
            MappedFileRWOutOfBounds
        }

        public MemoryLaneException() { }

        public MemoryLaneException(Code code, string msg = null) : base(msg) { ErrorCode = code; }

        public MemoryLaneException(string msg) : base(msg) { }
        
        public MemoryLaneException(string msg, Exception inner) : base(msg, inner) { }

        Code ErrorCode;
    }
}