using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
	public static class Print
	{
		public static void AsHelp(this string text, params object[] formatArgs) =>
			Trace(text, ConsoleColor.DarkCyan, null, formatArgs);

		public static void AsSysyemTrace(this string text, params object[] formatArgs) =>
			Trace(text, ConsoleColor.White, ConsoleColor.DarkBlue, formatArgs);


		public static void AsInfo(this string text, params object[] formatArgs)
		{
			if (IgnoreAll || IgnoreInfo) return;

			Console.WriteLine(text, formatArgs);
		}

		public static void AsSuccess(this string text, params object[] formatArgs)
		{
			if (IgnoreAll || IgnoreInfo) return;

			Trace(text, ConsoleColor.Green, null, formatArgs);
		}

		public static void AsInnerInfo(this string text, params object[] formatArgs)
		{
			if (IgnoreAll || IgnoreInfo) return;

			Console.WriteLine("    " + text, formatArgs);
		}

		public static void AsError(this string text, params object[] formatArgs) =>
			Trace(text, ConsoleColor.Red, null, formatArgs);

		public static void AsWarn(this string text, params object[] formatArgs) =>
			Trace(text, ConsoleColor.Yellow, null, formatArgs);

		public static void Trace(this string text, ConsoleColor c, ConsoleColor? bg, params object[] formatArgs)
		{
			if (IgnoreAll) return;

			var cc = Console.ForegroundColor;
			var bc = Console.BackgroundColor;
			Console.ForegroundColor = c;
			if (bg.HasValue) Console.BackgroundColor = bg.Value;
			Console.WriteLine(text, formatArgs);
			Console.ForegroundColor = cc;
			Console.BackgroundColor = bc;
		}


		public static bool IgnoreAll = false;
		public static bool IgnoreInfo = false;

	}
}
