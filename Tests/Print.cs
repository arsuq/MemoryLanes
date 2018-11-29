using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
	public static class Print
	{
		public static void AsInfo(this string text, params object[] formatArgs)
		{
			Console.WriteLine(text, formatArgs);
		}

		public static void AsSuccess(this string text, params object[] formatArgs)
		{
			Trace(text, ConsoleColor.Green, formatArgs);
		}

		public static void AsInnerInfo(this string text, params object[] formatArgs)
		{
			Console.WriteLine("    " + text, formatArgs);
		}

		public static void AsError(this string text, params object[] formatArgs)
		{
			Trace(text, ConsoleColor.Red, formatArgs);
		}

		public static void AsWarn(this string text, params object[] formatArgs)
		{
			Trace(text, ConsoleColor.Yellow, formatArgs);
		}

		public static void Trace(this string text, ConsoleColor c, params object[] formatArgs)
		{
			var cc = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine(text, formatArgs);
			Console.ForegroundColor = cc;
		}
	}
}
