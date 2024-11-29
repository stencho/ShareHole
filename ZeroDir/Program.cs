using System.Drawing;
using System.Runtime.CompilerServices;
using ZeroDir.Configuration;

namespace ZeroDir {
    public static class Config {
        public static string config_dir = "config";

        public static ConfigWithExpectedValues server;
        public static ConfigWithUserValues shares;

        public static bool log_headers = false;

        public static bool use_html_file = false;
        public static bool use_css_file = false;

        public static new Dictionary<string, Dictionary<string, ConfigValue>> server_config_values = 
            new Dictionary<string, Dictionary<string, ConfigValue>>() {
                    { "server",
                        new Dictionary<string, ConfigValue>() {
                            { "prefix", new ConfigValue("localhost") },
                            { "port", new ConfigValue(8080) },
                            { "threads", new ConfigValue(32) },
                            { "passdir", new ConfigValue("loot") },
                            { "use_html_file", new ConfigValue(false) },
                            { "use_css_file", new ConfigValue(false) }
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

        span.emojitint { 
          color: transparent; 
          text-shadow: 0 0 0 rgb(254, 168, 234); 
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

        internal static void load_config() {

            Config.server = new ConfigWithExpectedValues(Config.server_config_values);

            if (Config.server["server"].ContainsKey("use_html_file")) {
                Config.use_css_file = Config.server["server"]["use_html_file"].get_bool();
                Logging.Config("Using HTML from disk");
            } else {
                Logging.Config("Using CSS from constant");
            }

            if (Config.server["server"].ContainsKey("use_css_file")) {
                Config.use_css_file = Config.server["server"]["use_css_file"].get_bool();
                Logging.Config("Using CSS from disk");
            } else {
                Logging.Config("Using CSS from constant");
            }

            Logging.Config($"Loaded server config");

            Config.shares = new ConfigWithUserValues("shares");

            foreach (var section in Config.shares.Keys) {
                if (!Config.shares[section].ContainsKey("path")) {
                    Logging.Warning($"Share \"{section}\" doesn't contain a 'path' variable. Removing.");
                    Config.shares.Remove(section);
                }
            }

            Config.shares.config_file.WriteAllValuesToConfig(Config.shares);

            if (Config.shares.share_count == 0) {
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
                Logging.Config($"Using {Path.GetFullPath(Config.config_dir)} as config directory");
                if (Directory.Exists(Path.GetFullPath(Config.config_dir))) {
                    Directory.SetCurrentDirectory(Path.GetFullPath(Config.config_dir));
                } else {
                    Directory.CreateDirectory(Path.GetFullPath(Config.config_dir));
                    Directory.SetCurrentDirectory(Path.GetFullPath(Config.config_dir));
                    Logging.Config("Config directory missing. Creating a new one and loading defaults.");
                }
            }

            Logging.Config($"Loading configuration");
            load_config();
            Logging.Config($"Configuration loaded, starting server!");

            //foreach (string section in CurrentConfig.shares.Keys) {
            servers.Add(new FolderServer());
                Thread server_thread = new Thread(new ParameterizedThreadStart(start_server));
                server_thread.Start(0.ToString());                
            //}

            while (true) {
                string line = Console.ReadLine();

                if (line == "restart") {
                    Logging.Warning("Restarting all servers!");
                    for (int i = 0; i < servers.Count; i++) {
                        servers[i].StopServer();
                    }

                    while (true) {
                        int n = 0;
                        for (int i = 0; i < servers.Count; i++) {
                            if (!servers[i].all_threads_stopped())
                                n++;
                        }
                        if (n == 0) break;
                    }

                    load_config();

                    for (int i = 0; i < servers.Count; i++) {
                        servers[i] = new FolderServer();
                        server_thread = new Thread(new ParameterizedThreadStart(start_server));
                        server_thread.Start(0.ToString());
                    }

                } else if (line == "threadstatus") {
                    for (int i = 0; i < servers.Count; i++) {
                        var port = Config.server["server"]["port"].get_int();
                        var p = Config.server["server"]["prefix"].ToString().Trim().Split(' ')[0];

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
                    
                } else if (line.StartsWith("$") && line.Contains('.') && line.Contains('=')) {
                    line = line.Remove(0, 1);
                    Config.server.config_file.ChangeValueByString(Config.server, line);
                } else if (line.StartsWith("#") && line.Contains('.') && line.Contains('=')) {
                    line = line.Remove(0, 1);
                    Config.shares.config_file.ChangeValueByString(Config.shares, line);
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
