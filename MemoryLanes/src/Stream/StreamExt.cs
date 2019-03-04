/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Threading.Tasks;

namespace System.IO
{
	public static class StreamExt
	{
		/// <summary>
		/// Reads from the source stream and writes into the target.
		/// </summary>
		/// <param name="target">The receiver</param>
		/// <param name="source"></param>
		/// <param name="count">Number of bytes to read.</param>
		/// <param name="spoon">A small reading buffer. If null, new byte[4000] is used.</param>
		/// <returns>A task to await</returns>
		public static async Task<int> ReadFrom(this Stream target, Stream source, int count, byte[] spoon = null)
		{
			if (target == null || source == null) throw new ArgumentNullException();
			if (spoon == null) spoon = new byte[4000];
			if (count < 1) return 0;

			var total = 0;
			var read = 0;
			var sip = 0;

			while (total < count)
			{
				sip = count - total;
				if (sip > spoon.Length) sip = spoon.Length;

				// The read amount could be smaller than the sip
				read = await source.ReadAsync(spoon, 0, sip).ConfigureAwait(false);

				// Nothing left to read, or if it's a network stream - the other side is gone
				if (read < 1) break;

				await target.WriteAsync(spoon, 0, read).ConfigureAwait(false);

				total += read;

				if (total >= count) break;
			}

			return total;
		}
	}
}
