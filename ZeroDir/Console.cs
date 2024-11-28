using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir {
    internal static class Logging {
        static String printing = "";
        enum LOG_TYPES { MSG, WRN, ERR, CNF }

        public static void Message(string text, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            lock (printing) {
                WriteColor("[MSG]", ConsoleColor.Green);
                var last_slash = callerfilename.Replace('\\', '/').LastIndexOf('/') + 1;
                WriteColor($"[{callerfilename.Replace('\\', '/').Substring(last_slash, callerfilename.Length - last_slash)}->{membername}] ", ConsoleColor.Green);
                Console.WriteLine(text);
            }
        }
        public static void Warning(string text, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            lock (printing) {
                WriteColor("[WRN]", ConsoleColor.Yellow);
                var last_slash = callerfilename.Replace('\\', '/').LastIndexOf('/') + 1;
                WriteColor($"[{callerfilename.Replace('\\', '/').Substring(last_slash, callerfilename.Length - last_slash)}->{membername}] ", ConsoleColor.Yellow);
                Console.WriteLine(text);
            }
        }
        public static void Config(string text, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            lock (printing) {
                WriteColor("[CNF]", ConsoleColor.Cyan);
                var last_slash = callerfilename.Replace('\\', '/').LastIndexOf('/') + 1;
                WriteColor($"[{callerfilename.Replace('\\', '/').Substring(last_slash, callerfilename.Length - last_slash)}->{membername}] ", ConsoleColor.Cyan);
                Console.WriteLine(text);
            }
        }
        public static void Error(string text, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            lock (printing) {
                WriteColor("[ERR]", ConsoleColor.Red);
                var last_slash = callerfilename.Replace('\\', '/').LastIndexOf('/') + 1;
                WriteColor($"[{callerfilename.Replace('\\', '/').Substring(last_slash, callerfilename.Length - last_slash)}->{membername}] ", ConsoleColor.Red);
                Console.WriteLine(text);
            }
        }
        public static void ErrorAndThrow(string text, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            lock (printing) {
                WriteColor("[ERR]", ConsoleColor.Red);
                var last_slash = callerfilename.Replace('\\', '/').LastIndexOf('/') + 1;
                WriteColor($"[{callerfilename.Replace('\\', '/').Substring(last_slash, callerfilename.Length - last_slash)}] ", ConsoleColor.Red);
                Console.WriteLine(text);
                throw new Exception($"[ERR][{callerfilename.Replace('\\', '/').Substring(last_slash, callerfilename.Length - last_slash)}->{membername}] {text}");
            }
        }

        public static void CustomTag(string text, string tag_text, ConsoleColor tag_color, [CallerFilePath] string callerfilename = "") {
            var last_slash = callerfilename.Replace('\\', '/').LastIndexOf('/') + 1;
            var stripped = callerfilename.Replace('\\', '/').Substring(last_slash, callerfilename.Length - last_slash);

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
