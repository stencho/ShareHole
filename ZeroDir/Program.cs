using System.Drawing;
using System.Runtime.CompilerServices;
using ZeroDir.Configuration;
using ZeroDir.DBThreads;

namespace ZeroDir {
    public static class CurrentConfig {
        public static string config_dir = "config";

        public static ConfigWithExpectedValues server;
        public static ConfigWithUserValues shares;

        public static bool use_html_file = false;
        public static bool use_css_file = false;

        internal static CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
        internal static CancellationToken cancellation_token => cancellation_token_source.Token;

        public static new Dictionary<string, Dictionary<string, ConfigValue>> server_config_values = 
            new Dictionary<string, Dictionary<string, ConfigValue>>() {
                    { "server",
                        new Dictionary<string, ConfigValue>() {
                            { "prefix", new ConfigValue("localhost") },
                            { "port", new ConfigValue(8080) },
                            { "passdir", new ConfigValue("loot") },
                            { "threads", new ConfigValue(4) },
                            { "use_html_file", new ConfigValue(false) },
                            { "use_css_file", new ConfigValue(false) },
                            { "log_level", new ConfigValue(1)} // 0 = off, 1 = high importance only, 2 = all
                        }
                    },

                    { "gallery",
                        new Dictionary<string, ConfigValue>() {
                            { "thumbnail_size", new ConfigValue(192) },
                            { "thumbnail_compression_quality", new ConfigValue(60) }
                        }
                    }
                };

        public static string base_html = """
        <!DOCTYPE>
        <html lang="en">
          <head>
            <meta charset="UTF-8">
            <link rel="stylesheet" href="base.css">
            <title>{page_title}</title>
          </head>
          <body>
            {page_content}
          </body>
        </html>
        """;

        public static string base_css= """
        img {
          max-width: 100%;
          max-height: 100vh;
          height: auto;
        }

        body { 
          background-color: #101010; 
          font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
        }

        .emojitint { 
          color: transparent; 
          text-shadow: 0 0 0 rgb(254, 168, 234); 
        }
        
        .galleryfoldertext { 
          font-size: 12px;
          color: rgb(242, 191, 241); 
        }
        
        #gallery {
            align-items: end;
            align-content: normal;        
        }
        
        .thumbnail {
            min-width: {thumbnail_size}px !important;
            min-height: {thumbnail_size}px !important;
            max-width: {thumbnail_size}px !important;
            max-height: {thumbnail_size}px !important;
            display: inline-block;
            text-align-last: center !important;
            vertical-align: middle;
            align-content: center;
        }
        .thumbnail:hover {
            background-color: rgb(141, 69, 139);
        }

        p.up { 
          font-size: 32; 
        }

        p.head {
          color: rgb(255, 255, 255) !important;
          font-size: 32;
        }

        p {
          font-size: 20;
          font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        }

        a { text-decoration: none; }
        a:link { color: rgb(242, 191, 241); }
        a:visited { color: rgb(163, 212, 239); }
        a:hover { color: rgb(141, 69, 139); }
        a:active { color: rgb(203, 115, 200); }
        """;
    }

    internal class Program {
        static List<FolderServer> servers = new List<FolderServer>();

        internal static void LoadConfig() {
            CurrentConfig.server = new ConfigWithExpectedValues(CurrentConfig.server_config_values);

            if (CurrentConfig.server["server"].ContainsKey("use_html_file")) {
                CurrentConfig.use_css_file = CurrentConfig.server["server"]["use_html_file"].get_bool();
                Logging.Config("Using HTML from disk");
            } else {
                Logging.Config("Using CSS from constant");
            }

            if (CurrentConfig.server["server"].ContainsKey("use_css_file")) {
                CurrentConfig.use_css_file = CurrentConfig.server["server"]["use_css_file"].get_bool();
                Logging.Config("Using CSS from disk");
            } else {
                Logging.Config("Using CSS from constant");
            }

            Logging.Config($"Loaded server config");

            CurrentConfig.shares = new ConfigWithUserValues("shares");

            foreach (var section in CurrentConfig.shares.Keys) {
                if (!CurrentConfig.shares[section].ContainsKey("path")) {
                    Logging.Warning($"Share \"{section}\" doesn't contain a 'path' variable. Removing.");
                    CurrentConfig.shares.Remove(section);
                }
            }

            CurrentConfig.shares.config_file.WriteAllValuesToConfig(CurrentConfig.shares);

            if (CurrentConfig.shares.share_count == 0) {
                Logging.Config($"No shares configured in shares file!");
                Logging.Config("Add one to the shares file in your config folder using this format:");

                var tmpcol = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Magenta;

                Logging.Config($"""                                 
                                 [music]
                                 path=W:\\_STORAGE\MUSIC
                                 show_directories=true
                                 extensions=ogg mp3 wav flac alac ape m4a wma jpg jpeg bmp png gif 
                                 """);

                Console.ForegroundColor = tmpcol;

                return;
            } else {
                Logging.Config($"Loaded shares");
            }
        }   

        static void Exit() {
            Logging.Warning("Shutting down!");

            for (int i = 0; i < servers.Count; i++) {
                servers[i].StopServer();
            }

            Logging.Config($"Flushing config");
            CurrentConfig.server.config_file.Flush();

            Logging.Message("Goodbye!");

            Console.CursorVisible = true;
            Console.ForegroundColor = ConsoleColor.White;

            System.Environment.Exit(0);
        }

        static void Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (args.Length > 0) {
                for (int i = 0; i < args.Length; i++) {
                    if (args[i] == "-c") {
                        i++;
                        string p = args[i];
                        Logging.Config($"Using {Path.GetFullPath(p)} as config directory");
                        if (Directory.Exists(Path.GetFullPath(p))) {
                            Directory.SetCurrentDirectory(Path.GetFullPath(p));
                        } else {
                            Logging.Config("Config directory missing. Creating a new one and loading defaults.");
                            Directory.CreateDirectory(Path.GetFullPath(p));
                            Directory.SetCurrentDirectory(Path.GetFullPath(p));
                        }
                    }
                }
            } else {
                Logging.Config($"Using {Path.GetFullPath(CurrentConfig.config_dir)} as config directory");
                if (Directory.Exists(Path.GetFullPath(CurrentConfig.config_dir))) {
                    Directory.SetCurrentDirectory(Path.GetFullPath(CurrentConfig.config_dir));
                } else {
                    Directory.CreateDirectory(Path.GetFullPath(CurrentConfig.config_dir));
                    Directory.SetCurrentDirectory(Path.GetFullPath(CurrentConfig.config_dir));
                    Logging.Config("Config directory missing. Creating a new one and loading defaults.");
                }
            }

            Logging.Config($"Loading configuration");
            LoadConfig();
            Logging.Config($"Configuration loaded, starting server!");

            servers.Add(new FolderServer());
                Thread server_thread = new Thread(new ParameterizedThreadStart(start_server));
                server_thread.Start(0.ToString());

            Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e) { Exit(); e.Cancel = true; };
            
            while (true) { 
                string line = Console.ReadLine();

                if (line == "restart") {
                    Logging.Warning("Restarting all servers!");
                    for (int i = 0; i < servers.Count; i++) {
                        servers[i].StopServer();
                    }

                    Logging.Config($"Re-loading configuration");
                    LoadConfig();
                    Logging.Config($"Configuration loaded, starting server!");

                    for (int i = 0; i < servers.Count; i++) {
                        servers[i] = new FolderServer();
                        server_thread = new Thread(new ParameterizedThreadStart(start_server));
                        server_thread.Start(0.ToString());
                    }

                } else if (line == "shutdown") {
                    Exit();
                    return;

                } else if (line == "threadstatus") {
                    for (int i = 0; i < servers.Count; i++) {
                        var port = CurrentConfig.server["server"]["port"].get_int();
                        var p = CurrentConfig.server["server"]["prefix"].ToString().Trim().Split(' ')[0];

                        if (p.StartsWith("http://")) p = p.Remove(0, 7);
                        if (p.StartsWith("https://")) p = p.Remove(0, 8);
                        if (p.EndsWith('/')) p = p.Remove(p.Length - 1, 1);

                        FolderServer s = servers[i];
                        Logging.Message($"[Server] {p}:{port}");
                        for (int n = 0; n < s.dispatch_threads.Length; n++) {
                            Thread t = s.dispatch_threads[n];
                            Logging.Message($"| [Name] {t.Name} [IsAlive] {t.IsAlive} [ThreadState] {t.ThreadState.ToString()}");
                        }
                    }
                    
                } else if (line != null && line.StartsWith("$") && line.Contains('.') && line.Contains('=')) {
                    line = line.Remove(0, 1);
                    CurrentConfig.server.config_file.ChangeValueByString(CurrentConfig.server, line);
                } else if (line != null && line.StartsWith("#") && line.Contains('.') && line.Contains('=')) {
                    line = line.Remove(0, 1);
                    CurrentConfig.shares.config_file.ChangeValueByString(CurrentConfig.shares, line);
                }
            }
        }

        static void start_server(object? id) {
            servers[servers.Count - 1].StartServer(id.ToString());
        }

        ~Program() {
            Console.CursorVisible = true;
            Console.ForegroundColor = ConsoleColor.White;
        }

    }
}
