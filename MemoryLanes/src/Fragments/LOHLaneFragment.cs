
namespace System
{
	public struct LOHFragment : IDisposable
	{
		public LOHFragment(Memory<byte> m, Action dtor)
		{
			if (dtor == null) throw new NullReferenceException("dtor");

			Memory = m;
			destructor = dtor;
		}

		public void Dispose()
		{
			if (destructor != null)
			{
				destructor();
				destructor = null;
				Memory = null;
			}
		}

		public Memory<byte> Memory;
		Action destructor;
	}
}