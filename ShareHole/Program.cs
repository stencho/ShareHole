using System.Drawing;
using ShareHole.Configuration;
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

        public static string base_html = """
        <!doctype html>
        <html lang="en">
          <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <link rel="stylesheet" href="base.css">
            <title>{page_title}</title>
            {style}
          </head>
          <body>
            {page_content}
            {script}
          </body>
        </html>
        """;

        public static string base_css = """
        :root {
            --main-color: {main_color};
            --main-color-dark: {main_color_dark};
            --secondary-color: {secondary_color};
            --secondary-color-dark: {secondary_color_dark};
            --text-color: {text_color};
            --background-color: {background_color};
            --secondary-background-color: {secondary_background_color};
        }

        /* COMMON */
        a { text-decoration: none; }
        a:link { color: var(--main-color);  }
        a:visited { color: var(--secondary-color) ; }
        a:hover { color: var(--main-color-dark); }
        a:active { color: var(--secondary-color-dark) ; }
        
        html {
            scrollbar-color: var(--main-color) var(--background-color);
            scrollbar-width: thin;

            margin: 0;
            height: 100%;
        }
                
        body { 
            color: var(--text-color);
            background-color: var(--background-color); 

            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            font-size: 16pt;

            margin: 0;

            height: auto;
        }

        img {
            max-width: 100%;
            max-height: 100vh;
            height: auto;
        }

        text {        
            font-family: 'Segoe UI Symbol', Tahoma, Geneva, Verdana, sans-serif; 
        }

        .converter-container {
            font-size: 16pt;
            font-family: 'Segoe UI Symbol', Tahoma, Geneva, Verdana, sans-serif !important;     
            display: flex;
        }

        .converter-text { 
            font-size: 12pt;
            align-content: center;
         }

        /* LIST */
        .file { 
            color: transparent; 
            text-shadow: 0 0 0 var(--main-color);  
            width: 100%;
            display: flex;
            flex-grow: 1;
        }

        .file:hover { 
            color: transparent; 
            background-color: var(--main-color);  
            text-shadow: 0 0 0 var(--background-color); 
            display: inline-block;
        }


        .list-item {
            width: 100%;
            height: auto;
            font-size: 16pt;
            display: flex; 
            justify-content: space-between;
        }

        a.list-item-link {
            display: flex;
            flex-grow: 1;
        }
        
        
        /* GALLERY */
        #gallery {
            align-items: end;
            align-content: normal;        
        }

        .gallery_folder {
            color: transparent; 
            text-shadow: 0 0 0 var(--main-color);              
        }

        .gallery_folder_text { 
            font-size: 12px;
            color: var(--main-color);  
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
            background-color: var(--main-color-dark);
        }

        p.head {
            color: var(--text-color);
            font-size: 22;
        }
                       
        
        /* MUSIC PLAYER */
        #music-list-container {
            overflow-y: auto;
            width:100%;
            height: 100%;
            margin: 0;
            box-shadow: inset -1px 0 0 var(--main-color); 
        }  
        
        #music-list {
            display: flow;
            position: absolute;
            width: 100%;
        }  
        #music-list.scrollbar-visible {
            width: calc(100% - 2px) !important;
            border-right: solid 2px var(--main-color); 
        }
        
        .music-list-item {
            width:100%;
            height: auto;
            font-size: 16pt;
            margin: 0;
        }

        """;

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

                    { "theme",
                        new Dictionary<string, ConfigValue>() {
                            { "main_color", new ConfigValue(Color.FromArgb(255, 242,191,241)) },
                            { "main_color_dark", new ConfigValue(Color.FromArgb(255, 203, 115, 200)) },

                            { "secondary_color", new ConfigValue(Color.FromArgb(255, 163, 212, 239)) },
                            { "secondary_color_dark", new ConfigValue(Color.FromArgb(255, 110, 180, 210)) },

                            { "text_color", new ConfigValue(Color.FromArgb(255, 235, 235, 235)) },

                            { "background_color", new ConfigValue(Color.FromArgb(255, 16,16,16)) },
                            { "secondary_background_color", new ConfigValue(Color.FromArgb(255, 69,28,69)) }
                        }
                    },

                    { "conversion",
                        new Dictionary<string, ConfigValue>() {
                            { "jpeg_compression", new ConfigValue(true) },
                            { "jpeg_quality", new ConfigValue(85) }
                        }
                    },

                    { "transcode", new Dictionary<string, ConfigValue>() {
                            { "use_variable_bit_rate", new ConfigValue(true) },
                            { "vbr_quality_factor", new ConfigValue(22) },
                            { "cbr_bit_rate", new ConfigValue(1000) },
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

            //THEME
            ConfigFileIO.comment_manager.AddBefore("theme", """
                UI color settings in R,G,B,A format
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

            ConfigFileIO.comment_manager.AddBefore("transcode", "use_variable_bit_rate", """
                Switch between using a variable or fixed bit rate to determine video quality and size
                It is recommended that you use a variable bit rate
                """);

            ConfigFileIO.comment_manager.AddBefore("transcode", "vbr_quality_factor", """
                Variable bit rate quality, lower values improve quality but increase file size
                Values around 18-25 are recommended
                """);

            ConfigFileIO.comment_manager.AddBefore("transcode", "cbr_bit_rate", """
                The bit rate of the MP4 transcoding process, in kb
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


        internal class Program {
            static List<ShareServer> servers = new List<ShareServer>();

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

                servers.Add(new ShareServer());
                Thread server_thread = new Thread(new ParameterizedThreadStart(start_server));
                server_thread.Start(0.ToString());

                Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e) { Exit(); e.Cancel = true; };

                while (true) {
                    string line = Console.ReadLine();
                    
                    /*if (line == "restart") {
                        Logging.Warning("Restarting all servers!");
                        for (int i = 0; i < servers.Count; i++) {
                            servers[i].StopServer();
                        }

                        //await Task.WhenAll(cancellation_token);

                        Logging.Config($"Re-loading configuration");
                        LoadConfig();
                        Logging.Config($"Configuration loaded, starting server!");

                        for (int i = 0; i < servers.Count; i++) {
                            Task.Run(() => {
                                servers[i] = new FolderServer();
                                start_server(i);                            
                            }, cancellation_token);
                        }

                    } else */if (line == "shutdown") {
                        Exit();
                        return;

                    } else if (line == "threadstatus") {
                        for (int i = 0; i < servers.Count; i++) {
                            var port = CurrentConfig.server["server"]["port"].ToInt();
                            var p = CurrentConfig.server["server"]["prefix"].ToString().Trim().Split(' ')[0];

                            if (p.StartsWith("http://")) p = p.Remove(0, 7);
                            if (p.StartsWith("https://")) p = p.Remove(0, 8);
                            if (p.EndsWith('/')) p = p.Remove(p.Length - 1, 1);

                            ShareServer s = servers[i];
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

            ~Program() {
                Console.CursorVisible = true;
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }
}
