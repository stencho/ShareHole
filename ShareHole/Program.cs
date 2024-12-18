using System.Drawing;
using System.Runtime.CompilerServices;
using ShareHole.Configuration;
using ShareHole.DBThreads;
using static ShareHole.Conversion.Video;
using static ShareHole.Logging;

namespace ShareHole {
    public static class CurrentConfig {
        public static string config_dir = "config";

        public static LogLevel LogLevel = LogLevel.HIGH_IMPORTANCE;

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
                            { "threads", new ConfigValue(100) },
                            { "transfer_buffer_size", new ConfigValue(512)},
                            { "use_html_file", new ConfigValue(false) },
                            { "use_css_file", new ConfigValue(false) },
                            { "log_level", new ConfigValue(1) } // 0 = off, 1 = high importance only, 2 = all
                            
                        }
                    },

                    { "conversion",
                        new Dictionary<string, ConfigValue>() {
                            { "jpeg_compression", new ConfigValue(true) },
                            { "jpeg_quality", new ConfigValue(85) }
                        }
                    },

                    { "transcode", new Dictionary<string, ConfigValue>() {
                            { "bit_rate_kb",new ConfigValue(1000)},
                            { "threads_per_video_conversion", new ConfigValue(4) }
                        } 
                    },

                    { "list",
                        new Dictionary<string, ConfigValue>() {
                            { "show_stream_button", new ConfigValue(true) },
                            { "show_convert_image_buttons", new ConfigValue(true) },
                            { "convert_images_automatically", new ConfigValue(false) },
                            { "convert_videos_automatically", new ConfigValue(false) },
                            { "convert_audio_automatically", new ConfigValue(false) }
                        }
                    },

                    { "gallery",
                        new Dictionary<string, ConfigValue>() {
                            { "thumbnail_size", new ConfigValue(192) },
                            { "thumbnail_compression", new ConfigValue(false) },
                            { "thumbnail_compression_quality", new ConfigValue(60) },

                            { "convert_images_automatically", new ConfigValue(true) },
                            { "convert_videos_automatically", new ConfigValue(true) },
                            { "convert_audio_automatically", new ConfigValue(true) }
                        }
                    }
                };

        public static void InitializeComments() {
            //SERVER
            ConfigFileIO.comment_manager.AddBefore("server",
                "General server settings");

            ConfigFileIO.comment_manager.AddBefore("server", "prefix", 
                "Specify which adapter and port to bind to");

            ConfigFileIO.comment_manager.AddBefore("server", "passdir", """
                The name of the first section of the URL, required to access shares
                For example: example.com:8080/loot/share
                """);

            ConfigFileIO.comment_manager.AddBefore("server", "threads", """
                The number of threads for handling requests and uploads 
                This includes thumbnails, so if you're using gallery mode, you may want to increase this
                """);

            ConfigFileIO.comment_manager.AddBefore("server", "transfer_buffer_size", """
                The size of each partial transfer chunk's buffer size in kilobytes
                """);

            ConfigFileIO.comment_manager.AddBefore("server", "use_html_file", """
                Look for base.html and base.css in the config directory instead of loading them from memory
                """);

            ConfigFileIO.comment_manager.AddBefore("server", "log_level", """
                0 = Logging off, 1 = high importance only, 2 = all messages
                """);


            //CONVERSION
            ConfigFileIO.comment_manager.AddBefore("conversion","""
                Settings for converting between different file types
                """);

            ConfigFileIO.comment_manager.AddBefore("conversion", "jpeg_compression", """                
                Toggle between lossless and compressed JPEG when using /to_jpg
                """);

            ConfigFileIO.comment_manager.AddBefore("conversion", "jpeg_quality", """
                Quality level, from 0-100
                """);

            //TRANSCODE
            ConfigFileIO.comment_manager.AddBefore("transcode", """
                Settings for transcoding video files to MP4 and streaming them over the network
                """);

            ConfigFileIO.comment_manager.AddBefore("transcode", "bit_rate_kb", """
                The bitrate of the MP4 transcoding process, in KB
                """);

            ConfigFileIO.comment_manager.AddBefore("transcode", "threads_per_video_conversion", """
                Determines how many threads are started for each /transcode/
                """);


            //LIST
            ConfigFileIO.comment_manager.AddBefore("list",
                "Settings for the default \"list\" share style");

            ConfigFileIO.comment_manager.AddBefore("list", "show_stream_button", """
                Display a play button next to video files, which when clicked will transcode the video
                to x264 MP4 and stream that to the client, from start to finish
                Seeking while the file is loading is possible in FireFox, but not Chrome
                """);

            ConfigFileIO.comment_manager.AddBefore("list", "show_convert_image_buttons", """                
                Display "PNG" and "JPG" buttons next to certain files which normally wouldn't be renderable in browser
                """);

            ConfigFileIO.comment_manager.AddBefore("list", "convert_images_automatically", """                
                Will modify URLs in the list to point to, for example, /to_jpg/ when the file is a .dng RAW
                the others do the same thing but for video/audio
                """);
            

            //GALLERY
            ConfigFileIO.comment_manager.AddBefore("gallery", """
                Settings for the 'gallery' view style
                """);
            ConfigFileIO.comment_manager.AddBefore("gallery", "thumbnail_size", """
                Thumbnail maximum resolution for both x and y axes
                """);
            ConfigFileIO.comment_manager.AddBefore("gallery", "thumbnail_compression", """
                true = JPEG thumbnails, false = PNG thumbnails, prettier, but uses more data
                """);
            ConfigFileIO.comment_manager.AddBefore("gallery", "thumbnail_compression_quality", """
                JPEG compression quality; 0-100
                """);
            ConfigFileIO.comment_manager.AddBefore("gallery", "convert_images_automatically", """                
                Does the same thing as the options in [list], but for the gallery
                On by default
                """);
        }

        public static string base_html = """
        <!DOCTYPE>
        <html lang="en">
          <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
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

        text {        
          color: rgb(235, 235, 235);
          font-family: 'Segoe UI Symbol', Tahoma, Geneva, Verdana, sans-serif; 
        }
        text.list_extra {        
          font-family: 'Segoe UI Symbol', Tahoma, Geneva, Verdana, sans-serif; 
          font-size: 12pt;
        }

        body { 
          color: rgb(235, 235, 235);
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
          font-size: 22;
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
            CurrentConfig.InitializeComments();

            CurrentConfig.server = new ConfigWithExpectedValues(CurrentConfig.server_config_values);
            
            if (CurrentConfig.server["server"].ContainsKey("use_html_file")) {
                CurrentConfig.use_css_file = CurrentConfig.server["server"]["use_html_file"].ToBool();
                Logging.Config("Using HTML from disk");
            } else {
                Logging.Config("Using CSS from constant");
            }

            if (CurrentConfig.server["server"].ContainsKey("use_css_file")) {
                CurrentConfig.use_css_file = CurrentConfig.server["server"]["use_css_file"].ToBool();
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

            //if (!NetworkCache.currently_pruning) {
            //    NetworkCache.StartPruning();
            //}

            CurrentConfig.LogLevel = (LogLevel)CurrentConfig.server["server"]["log_level"].ToInt();
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
                        var port = CurrentConfig.server["server"]["port"].ToInt();
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
