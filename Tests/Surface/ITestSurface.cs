using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tests.Surface
{
	using ArgMap = IDictionary<string, List<string>>;

	public interface ITestSurface
	{
		Task Run(ArgMap args);
		string Info();
	}
}
