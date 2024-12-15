using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir {
    internal static class Logging {
        static String printing = "";
        enum LOG_TYPES { MSG, WRN, ERR, CNF }

        public enum LogLevel {
            OFF = 0,
            HIGH_IMPORTANCE = 1,
            ALL = 2
        };

        public static LogLevel CurrentLogLevel = LogLevel.HIGH_IMPORTANCE;

        public static void Message(string text, bool show_caller=true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "MSG", ConsoleColor.Green, show_caller, callerfilename, membername);
        }
        public static void Warning(string text, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "WRN", ConsoleColor.Yellow, show_caller, callerfilename, membername);
        }
        public static void Config(string text, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "CFG", ConsoleColor.Cyan, show_caller,callerfilename, membername);
        }
        public static void Error(string text, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "ERR", ConsoleColor.Red, show_caller, callerfilename, membername);
        }

        public static void ThreadMessage(string text, string thread_name, int thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            LogExtra(text, "MSG", ConsoleColor.Green, thread_name, SeededRandomConsoleColor(thread_id), show_caller, callerfilename, membername);
        }
        public static void ThreadWarning(string text, string thread_name, int thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            LogExtra(text, "WRN", ConsoleColor.Yellow, thread_name, SeededRandomConsoleColor(thread_id), show_caller, callerfilename, membername);
        }
        public static void ThreadConfig(string text, string thread_name, int thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            LogExtra(text, "CFG", ConsoleColor.Cyan, thread_name, SeededRandomConsoleColor(thread_id), show_caller, callerfilename, membername);
        }
        public static void ThreadError(string text, string thread_name, int thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            LogExtra(text, "ERR", ConsoleColor.Red, thread_name, SeededRandomConsoleColor(thread_id), show_caller, callerfilename, membername);
        }

        public static void Custom(string text, string tag, ConsoleColor tag_color, bool show_caller = false, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, tag, tag_color, show_caller);
        }
        public static void CustomDouble(string text, string tag, ConsoleColor tag_color, string second_tag, ConsoleColor second_tag_color, bool show_caller = false, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            LogExtra(text, tag, tag_color, second_tag, second_tag_color, show_caller);
        }

        public static void ErrorAndThrow(string text, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "ERR", ConsoleColor.Red, show_caller, callerfilename, membername);
            throw new Exception($"{text}");            
        }

        static void Log(string text, string tag, ConsoleColor color, bool show_caller = true, string caller_fn = "", string caller_mn = "") {
            lock (printing) {
                WriteColor($"[{tag}]", color);
                if (show_caller) {
                    var last_slash = caller_fn.Replace('\\', '/').LastIndexOf('/') + 1;
                    var fn = caller_fn.Replace('\\', '/').Substring(last_slash, caller_fn.Length - last_slash);
                    fn = fn.Remove(fn.Length - 3);
                    WriteColor($"[{fn}->{caller_mn}] ", color);
                } else Console.Write(" ");
                Console.WriteLine(text);
            }
        }

        static void LogExtra(string text, string tag, ConsoleColor color, string extra_tag, ConsoleColor extra_color, bool show_caller = true, string caller_fn = "", string caller_mn = "") {
            lock (printing) {
                WriteColor($"[{tag}]", color);
                if (show_caller) {
                    var last_slash = caller_fn.Replace('\\', '/').LastIndexOf('/') + 1;
                    var fn = caller_fn.Replace('\\', '/').Substring(last_slash, caller_fn.Length - last_slash);
                    fn = fn.Remove(fn.Length - 3);
                    WriteColor($"[{fn}->{caller_mn}] ", color);
                } else Console.Write(" ");
                WriteColor($"[{extra_tag}] ", extra_color);
                Console.WriteLine(text);
            }
        }

        public static void CustomTag(string text, string tag_text, ConsoleColor tag_color, [CallerFilePath] string callerfilename = "") {
            var last_slash = callerfilename.Replace('\\', '/').LastIndexOf('/') + 1;
            var stripped = callerfilename.Replace('\\', '/').Substring(last_slash, callerfilename.Length - last_slash);

        }

        public static void WriteLineColor(string str, ConsoleColor color) {
            Console.ForegroundColor = color;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void WriteColor(string str, ConsoleColor color) {
            Console.ForegroundColor = color;
            Console.Write(str);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static ConsoleColor RandomConsoleColor() {
            var cc_list = Enum.GetNames(typeof(ConsoleColor));
            var cc_count = cc_list.Length;
            var rng = Random.Shared.Next(0, cc_count);
            
            return (ConsoleColor)Enum.Parse(typeof(ConsoleColor), cc_list[rng]);
        }
        public static ConsoleColor SeededRandomConsoleColor(int seed) {
            var cc_list = Enum.GetNames(typeof(ConsoleColor));
            var cc_count = cc_list.Length;
            var rng = new Random(seed).Next(0, cc_count);

            return (ConsoleColor)Enum.Parse(typeof(ConsoleColor), cc_list[rng]);
        }
    }
}
