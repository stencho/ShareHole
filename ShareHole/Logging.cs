using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ShareHole {
    public static class Logging {
        enum LOG_TYPES { MSG, WRN, ERR, CNF }

        public enum LogLevel {
            OFF = 0,
            HIGH_IMPORTANCE = 1,
            ALL = 2
        };

        struct LogItem {
            string text;

            string tag;
            ConsoleColor tag_color;

            string second_tag;
            ConsoleColor second_tag_color;

            bool show_caller = false;
            string caller_file_name = "";
            string caller_member_name = "";

            public LogItem(string text, string tag, ConsoleColor tag_color, string second_tag, ConsoleColor second_tag_color, bool show_caller, string callerfilename, string membername) {
                this.text = text;
                this.tag = tag;
                this.tag_color = tag_color;
                this.second_tag = second_tag;
                this.second_tag_color = second_tag_color;
                this.show_caller = show_caller;
                this.caller_file_name = callerfilename;
                this.caller_member_name = membername;
            }

            public void print() {
                if (string.IsNullOrEmpty(text)) return;

                //draw caller tag
                if (show_caller) {
                    var last_slash = caller_file_name.Replace('\\', '/').LastIndexOf('/') + 1;
                    var fn = caller_file_name.Replace('\\', '/').Substring(last_slash, caller_file_name.Length - last_slash);
                    fn = fn.Remove(fn.Length - 3);
                    Logging.WriteColor($"[{fn}->{caller_member_name}] ", tag_color);

                    //draw tag
                } else if (!string.IsNullOrEmpty(tag)) 
                    Logging.WriteColor($"[{tag}] ", tag_color);
                else 
                    Console.Write(" ");

                //draw second/"thread" tag
                if (!string.IsNullOrEmpty(second_tag)) {
                    Logging.WriteColor($"[{second_tag}] ", second_tag_color);
                }
                Console.WriteLine(text);
            }

        };

        static ConcurrentQueue<LogItem> LogQueue = new ConcurrentQueue<LogItem>();

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
            Log(text, "MSG", ConsoleColor.Green, show_caller, callerfilename, membername, thread_name, SeededRandomConsoleColor(thread_id));
        }
        public static void ThreadWarning(string text, string thread_name, int thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "WRN", ConsoleColor.Yellow, show_caller, callerfilename, membername, thread_name, SeededRandomConsoleColor(thread_id));
        }
        public static void ThreadConfig(string text, string thread_name, int thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "CFG", ConsoleColor.Cyan, show_caller, callerfilename, membername, thread_name, SeededRandomConsoleColor(thread_id));
        }
        public static void ThreadError(string text, string thread_name, int thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "ERR", ConsoleColor.Red, show_caller, callerfilename, membername, thread_name, SeededRandomConsoleColor(thread_id));
        }
        public static void ThreadMessage(string text, string thread_name, long thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "MSG", ConsoleColor.Green, show_caller, callerfilename, membername, thread_name, SeededRandomConsoleColor(thread_id));
        }
        public static void ThreadWarning(string text, string thread_name, long thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "WRN", ConsoleColor.Yellow, show_caller, callerfilename, membername, thread_name, SeededRandomConsoleColor(thread_id));
        }
        public static void ThreadConfig(string text, string thread_name, long thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "CFG", ConsoleColor.Cyan, show_caller, callerfilename, membername, thread_name, SeededRandomConsoleColor(thread_id));
        }
        public static void ThreadError(string text, string thread_name, long thread_id, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "ERR", ConsoleColor.Red, show_caller, callerfilename, membername, thread_name, SeededRandomConsoleColor(thread_id));
        }

        public static void Custom(string text, string tag, ConsoleColor tag_color, bool show_caller = false, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, tag, tag_color, show_caller, callerfilename, membername);
        }
        public static void CustomDouble(string text, string tag, ConsoleColor tag_color, string second_tag, ConsoleColor second_tag_color, bool show_caller = false, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, tag, tag_color, show_caller, callerfilename, membername, second_tag, second_tag_color);
        }

        public static void ErrorAndThrow(string text, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "ERR", ConsoleColor.Red, show_caller, callerfilename, membername);
            throw new Exception($"{text}");            
        }

        static void Log(string text, string tag, ConsoleColor color, bool show_caller = true, string caller_fn = "", string caller_mn = "", string extra_tag = "", ConsoleColor extra_color = ConsoleColor.White) {
            if (State.LogLevel == 0) return;
            LogQueue.Enqueue(new LogItem(text, tag, color, extra_tag, extra_color, show_caller, caller_fn, caller_mn));
        }

        static bool running = false;
        static CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
        static CancellationToken cancellation_token => cancellation_token_source.Token;
        public static void Start() {
            if (!running) {
                running = true;
                Task.Run(ProcessQueue, cancellation_token);
            }
        }
        public static void Stop() {      
            cancellation_token_source.Cancel();

            while (running) { Thread.Sleep(50); }
            
            finish_queue();
        }

        static void ProcessQueue() {
            while (running && !cancellation_token.IsCancellationRequested) {
                LogItem li;

                if (LogQueue.TryDequeue(out li)) li.print();
                else Thread.Sleep(10);
            }
            running = false;
        }

        static void finish_queue() {
            while (LogQueue.Count > 0) {
                LogItem li;

                if (LogQueue.TryDequeue(out li)) li.print();
                else Thread.Sleep(10);
            }
        }

        internal static void WriteColor(string str, ConsoleColor color) {
            if (State.LogLevel == 0) return;
            Console.ForegroundColor = color;
            Console.Write(str);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static ConsoleColor RandomConsoleColor() {
            var cc_list = Enum.GetNames(typeof(ConsoleColor));
            var cc_count = cc_list.Length;
            var rng = Random.Shared.Next(1, cc_count-1);
                        
            return (ConsoleColor)Enum.Parse(typeof(ConsoleColor), cc_list[rng]);
        }
        public static ConsoleColor SeededRandomConsoleColor(long seed) {
            var cc_list = Enum.GetNames(typeof(ConsoleColor));
            var cc_count = cc_list.Length;
            var rng = new Random((int)seed).Next(1, cc_count-1);

            return (ConsoleColor)Enum.Parse(typeof(ConsoleColor), cc_list[rng]);
        }
    }
}
