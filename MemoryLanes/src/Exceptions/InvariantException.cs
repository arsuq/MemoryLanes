namespace System
{
	public class InvariantException : Exception
	{
		public InvariantException() { }

		public InvariantException(string invariant) : this(invariant, string.Empty) { }

		public InvariantException(string invariant, string message) : base(message)
		{
			Invariant = invariant;
		}

		public readonly string Invariant;
	}
}
