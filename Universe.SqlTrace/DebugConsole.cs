using System;
using System.Diagnostics;

namespace Universe.Utils
{
    internal class DebugConsole
    {
        [Conditional("DEBUG")]
        public static void WriteLine(string message)
        {
            Debug.WriteLine(message);
        }

        [Conditional("DEBUG")]
        public static void WriteLine(string messageFormat, params object[] args)
        {
            WriteLine(string.Format(messageFormat, args));
        }

        [Conditional("DEBUG")]
        public static void WriteException(Exception ex, string header)
        {
            WriteLine(header + Environment.NewLine + ex);
        }

        [Conditional("DEBUG")]
        public static void WriteException(Exception ex, string headerFormat, params object[] args)
        {
            WriteLine(string.Format(headerFormat, args) + Environment.NewLine + ex);
        }

    }

}