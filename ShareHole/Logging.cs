using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ShareHole {
    public static class Logging {

        enum LOG_TYPES { MSG, WRN, ERR, CNF }

        public enum LogLevel {
            OFF = 0,
            HIGH_IMPORTANCE = 1,
            ALL = 2
        };

        struct log_item {
            string text;

            string tag;
            ConsoleColor tag_color;

            string second_tag;
            ConsoleColor second_tag_color;

            bool show_caller = false;
            string caller_file_name = "";
            string caller_member_name = "";


            public log_item(string text, string tag, ConsoleColor tag_color, string second_tag, ConsoleColor second_tag_color, bool show_caller, string callerfilename, string membername) {
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
                var l = 0;
                //draw caller tag
                if (show_caller) {
                    var last_slash = caller_file_name.Replace('\\', '/').LastIndexOf('/') + 1;
                    var fn = caller_file_name.Replace('\\', '/').Substring(last_slash, caller_file_name.Length - last_slash);
                    fn = fn.Remove(fn.Length - 3);
                    Logging.WriteColor($"[{fn}->{caller_member_name}] ", tag_color);
                    l += $"[{fn}->{caller_member_name}] ".Length;
                    //draw tag
                } else if (!string.IsNullOrEmpty(tag)) {
                    Logging.WriteColor($"[{tag}] ", tag_color);
                    l += $"[{tag}] ".Length;
                } else {
                    Console.Write(" ");
                    l++;
                }

                //draw second/"thread" tag
                if (!string.IsNullOrEmpty(second_tag)) {
                    Logging.WriteColor($"[{second_tag}] ", second_tag_color);
                    l += $"[{second_tag}] ".Length;
                }

                Console.Write(text);
                l += text.Length;

                Console.Write(new string(' ', Console.WindowWidth - (l % Console.WindowWidth)));
            }

        };

        static ConcurrentQueue<log_item> LogQueue = new ConcurrentQueue<log_item>();

        static ConcurrentQueue<string> LogText = new ConcurrentQueue<string>();

        static void Log(string text, string tag, ConsoleColor color, bool show_caller = true, string caller_fn = "", string caller_mn = "", string extra_tag = "", ConsoleColor extra_color = ConsoleColor.White) {
            if (State.LogLevel == 0) return;
            LogQueue.Enqueue(new log_item(text, tag, color, extra_tag, extra_color, show_caller, caller_fn, caller_mn));

            string s = "";
            //draw caller tag
            if (show_caller) {
                var last_slash = caller_fn.Replace('\\', '/').LastIndexOf('/') + 1;
                var fn = caller_fn.Replace('\\', '/').Substring(last_slash, caller_fn.Length - last_slash);
                fn = fn.Remove(fn.Length - 3);
                s += $"[{caller_fn}->{caller_mn}] ";

                //draw tag
            } else if (!string.IsNullOrEmpty(tag))
                s += $"[{tag}] ";
            else
                Console.Write(" ");

            //draw second/"thread" tag
            if (!string.IsNullOrEmpty(extra_tag)) {
                s += $"[{extra_tag}] ";
            }

            s += text;
            LogText.Enqueue(s);
        }

        static bool running = false;
        static CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
        static CancellationToken cancellation_token => cancellation_token_source.Token;

        public static void Start() {
            if (!running) {
                running = true;
                Task.Run(ProcessQueue, cancellation_token);
                Console.CursorVisible = false;
                find_processor_usage();
            }
        }
        public static void Stop() {      
            cancellation_token_source.Cancel();
            while (running) { Thread.Sleep(50); }
            
            finish_queue();

            Console.CursorVisible = true;
            Console.CursorLeft = 0;
            Console.CursorTop = Console.WindowTop + Console.WindowHeight;
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
        }


        static void print_status_bar_top() {
            int stored_x = Console.CursorLeft;
            int stored_y = Console.CursorTop;
            int top = Console.WindowTop;

            string status_text = $"Status: {DateTime.Now}";

            Console.SetCursorPosition(0, top);

            Console.BackgroundColor = ConsoleColor.Magenta;
            Console.ForegroundColor = ConsoleColor.Black;

            Console.Write(status_text);
            Console.Write(new string(' ', Console.WindowWidth - status_text.Length));

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;

            Console.CursorLeft = stored_x;
            Console.CursorTop = stored_y;
        }

        static int keyboard_text_start = 0;

        static string keyboard_input_buffer = "";
        public static string KeyboardBuffer => keyboard_input_buffer;

        static int LastConsoleBottom = 0;
        static int ConsoleBottom => Console.WindowTop + Console.WindowHeight - 1;
        static int keyboard_input_cursor = 0;

        public static Action<string> HandleReadLineAction;

        public static bool enable_info_bar => !force_disable_info_bar && State.server["server"]["show_info"].ToBool();
        public static bool log_to_queue => State.server["server"]["log_to_queue"].ToBool();
        public static bool force_disable_info_bar = false;
        static void invert() {
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.White;
        }
        static void uninvert() {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
        }
        static int full_status_length = 0;

        static double cpu_usage = 0.0;
        static async void find_processor_usage() {
            while (!cancellation_token.IsCancellationRequested) {
                var proc = Process.GetCurrentProcess();
                int cores = Environment.ProcessorCount;

                var start_time_cpu = proc.TotalProcessorTime;
                var start_time_real = DateTime.Now;

                await Task.Delay(1000);

                var end_time_cpu = proc.TotalProcessorTime;
                var end_time_real = DateTime.Now;

                var cpu_td = (end_time_cpu - start_time_cpu).TotalSeconds;
                var real_td = (end_time_real - start_time_real).TotalSeconds;

                cpu_usage = (cpu_td / (real_td * cores)) * 100.0;
            }
        }

        static void print_status_bar_bottom() {
            keyboard_text_start = 0;
            int stored_x = Console.CursorLeft;
            int stored_y = Console.CursorTop;
            int top = Console.WindowTop;

            var me = Process.GetCurrentProcess();
                        
            string status_text = $" CPU: {string.Format("{0:0.00}", cpu_usage)}% RAM: {me.PagedMemorySize / 1000 / 1000}MB Threads: {Program.server.running_request_threads}->{State.TaskCount} Cache: {ThumbnailManager.ThumbsInCache} thumbs ";
            keyboard_text_start += status_text.Length;
            full_status_length += status_text.Length;

            Console.CursorTop = Console.WindowTop + Console.WindowHeight - 1;
            Console.CursorLeft = 0;

            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Magenta;

            Console.Write(status_text);

            string cmd_txt = "";
            var dkl = 0;            

            if (keyboard_input_buffer.Length > 0) {
                uninvert();
                cmd_txt = $" Command: ";
                Console.Write(cmd_txt);
                full_status_length += cmd_txt.Length;
                keyboard_text_start += " Command: ".Length;
                dkl = draw_keyboard_buffer_with_cursor();
                full_status_length += dkl;
            }

            full_status_length = status_text.Length + cmd_txt.Length + dkl;

            uninvert();

            Console.Write(new string(' ', Console.WindowWidth - full_status_length));

            Console.CursorLeft = stored_x;
            Console.CursorTop = stored_y; 
            
        }

        static int draw_keyboard_buffer_with_cursor() {
            int l = 0;

            if (keyboard_text_start > Console.WindowWidth) return 0;
            Console.CursorLeft = keyboard_text_start;
            if ((keyboard_input_buffer.Length < 1)) {
                invert();
                Console.Write(' ');
                uninvert();
                return 1;
            }  else if (keyboard_input_cursor >= keyboard_input_buffer.Length) {
                Console.Write(keyboard_input_buffer);
                invert();
                Console.Write(' ');
                uninvert();
                return keyboard_input_buffer.Length + 1;
            } else if (keyboard_input_buffer.Length == 1) {
                invert();
                Console.Write(keyboard_input_buffer);
                uninvert();
                return keyboard_input_buffer.Length;
            } 

            //Console.CursorTop = Console.WindowTop + Console.WindowHeight - 1;

            string before_cursor = keyboard_input_buffer.Substring(0, keyboard_input_cursor);
            string after_cursor = keyboard_input_buffer.Substring(keyboard_input_cursor+1, keyboard_input_buffer.Length - (keyboard_input_cursor+1));
            char cursor = keyboard_input_buffer[keyboard_input_cursor];

            Console.Write(before_cursor);
            invert();
            Console.Write(cursor);
            uninvert();
            Console.Write(after_cursor);

            return keyboard_input_buffer.Length;
        }

        public static void ProcessKeyboard() {
            var key = Console.ReadKey(true);
            
            // letters, numbers, special chars
            if (key.KeyChar >= 32 && key.KeyChar <= 126 && keyboard_input_buffer.Length <= Console.WindowWidth - keyboard_text_start-5) {
                keyboard_input_buffer = keyboard_input_buffer.Insert(keyboard_input_cursor, key.KeyChar.ToString());
                keyboard_input_cursor++;
            
            } else if (key.Key == ConsoleKey.Delete) {
                if (keyboard_input_cursor < keyboard_input_buffer.Length)
                    keyboard_input_buffer = keyboard_input_buffer.Remove(keyboard_input_cursor, 1);
                            
            } else if (key.Key == ConsoleKey.Backspace) {
                keyboard_input_cursor--;
                if (keyboard_input_cursor < 0) keyboard_input_cursor = 0;
                else keyboard_input_buffer = keyboard_input_buffer.Remove(keyboard_input_cursor);
                            
            } else if (key.Key == ConsoleKey.Enter) {                
                HandleReadLineAction.Invoke(keyboard_input_buffer);
                
                keyboard_input_buffer = "";
                keyboard_input_cursor = 0;
            } else if (key.Key == ConsoleKey.RightArrow) {
                keyboard_input_cursor++;
            } else if (key.Key == ConsoleKey.LeftArrow) {                
                keyboard_input_cursor--;
                if (keyboard_input_cursor < 0) keyboard_input_cursor = 0;
            }
            if (enable_info_bar) print_status_bar_bottom();
            else {
                var i = draw_keyboard_buffer_with_cursor();
                Console.Write(new string(' ', Console.WindowWidth - i - 1));
            }

        }

        static void ProcessQueue() {            
            while (running && !cancellation_token.IsCancellationRequested) {
                log_item li;
                if (LogQueue.Count > 0) {
                    Console.CursorLeft = 0;

                keep_going:
                    if (LogQueue.TryDequeue(out li)) {
                        li.print();
                    }
                    if (LogQueue.Count > 0) goto keep_going;
                    Console.WriteLine();

                    if (enable_info_bar) print_status_bar_bottom();
                    //else {
                    //    var i = draw_keyboard_buffer_with_cursor();
                    //    Console.Write(new string(' ', Console.WindowWidth - i - 1));
                    //}
                    LastConsoleBottom = ConsoleBottom;

                } else Thread.Sleep(200);

                if (enable_info_bar && LastConsoleBottom <= ConsoleBottom && keyboard_input_buffer.Length == 0) {
                    Console.CursorTop = ConsoleBottom;
                    print_status_bar_bottom();
                } 

            }

            running = false;
        }

        static void finish_queue() {
            while (LogQueue.Count > 0) {
                log_item li;

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
        public static void Message(string text, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "MSG", ConsoleColor.Green, show_caller, callerfilename, membername);
        }
        public static void Warning(string text, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "WRN", ConsoleColor.Yellow, show_caller, callerfilename, membername);
        }
        public static void Config(string text, bool show_caller = true, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
            Log(text, "CFG", ConsoleColor.Cyan, show_caller, callerfilename, membername);
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
    }
}
