/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
