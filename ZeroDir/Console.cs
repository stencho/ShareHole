using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir {
    internal static class Logging {
        public static void Info(string text) {
            WriteColor("[Info] ", ConsoleColor.Green); Console.WriteLine(text);
        }
        public static void Warning(string text) {
            WriteColor("[Warning] ", ConsoleColor.Yellow); Console.WriteLine(text);
        }
        public static void Error(string text) {
            WriteColor("[Error] ", ConsoleColor.Red); Console.WriteLine(text);
        }
        public static void ErrorAndThrow(string text) {
            WriteColor("[Error] ", ConsoleColor.Red); Console.WriteLine(text);
            throw new Exception(text);
        }

        public static void WriteLineColor(string str, ConsoleColor color) {
            var tmp = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(str);
            Console.ForegroundColor = tmp;
        }

        public static void WriteColor(string str, ConsoleColor color) {
            var tmp = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(str);
            Console.ForegroundColor = tmp;
        }
    }
}
