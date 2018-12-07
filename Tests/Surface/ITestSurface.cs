using System.Collections.Generic;

namespace Tests.Surface
{
	using ArgMap = IDictionary<string, List<string>>;

	public interface ITestSurface
	{
		void Run(ArgMap args);
		string Info();
	}
}
