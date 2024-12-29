using ChoonGang;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.FileProviders;
using ShareHole;
using ShareHole.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web;

namespace ChoonGang;
public static class Tasks {

    static int _task_count = 0;
    public static int TaskCount => _task_count;

    static void IncrementTaskCount() => Interlocked.Increment(ref _task_count);
    static void DecrementTaskCount() => Interlocked.Decrement(ref _task_count);



    internal static CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
    internal static CancellationToken cancellation_token => cancellation_token_source.Token;

    public static void reset_cancellation_token() {
        cancellation_token_source = new CancellationTokenSource();
    }

    public static Task StartTask(Action action, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
        Guid task_guid = Guid.NewGuid();

        return Task.Run(() => {
            IncrementTaskCount();
            try {
                action.Invoke();
            } finally {
                DecrementTaskCount();
            }
        }, cancellation_token).ContinueWith(t => {
            if (t.IsFaulted) {
                Logging.Error($"Task failed: ");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public static Task StartTask(Action action, CancellationToken cancellation_token, [CallerFilePath] string callerfilename = "", [CallerMemberName] string membername = "") {
        Guid task_guid = Guid.NewGuid();

        return Task.Run(() => {
            IncrementTaskCount();
            try {
                action.Invoke();
            } finally {
                DecrementTaskCount();
            }
        }, cancellation_token).ContinueWith(t => {
            if (t.IsFaulted) {
                Logging.Error($"Task failed: ");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}

public static class State {
    public static string config_dir = "config";

    public static string Prefixes => server["server"]["prefix"].ToString();
    public static int Port => server["server"]["port"].ToInt();
    public static int RequestThreads => server["server"]["threads"].ToInt();

    public static void SetTitle(string status) {
        Console.Title = "[Choon Gang] " + status;
    }

    public static ConfigWithExpectedValues server;
    public static new Dictionary<string, Dictionary<string, ConfigValue>> server_config_values =
        new Dictionary<string, Dictionary<string, ConfigValue>>() {
                    { "server",
                        new Dictionary<string, ConfigValue>() {
                            { "prefix", new ConfigValue("localhost") },
                            { "port", new ConfigValue(8080) },
                            { "path", new ConfigValue("YOUR MUSIC FOLDER") },
                            { "threads", new ConfigValue(8) },
                            { "transfer_buffer_size", new ConfigValue(512)}
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
        };

    public static void InitializeComments() {
        //SERVER
        ConfigFileIO.comment_manager.AddBefore("server",
            "General server settings");

        ConfigFileIO.comment_manager.AddBefore("server", "prefix",
            "The adapter and port for the server to bind to");

        ConfigFileIO.comment_manager.AddBefore("server", "path", """
                Your music directory
                """);

        ConfigFileIO.comment_manager.AddBefore("server", "threads", """
                The number of threads for handling requests 
                """);

        ConfigFileIO.comment_manager.AddBefore("server", "transfer_buffer_size", """
                The size of each partial transfer chunk's buffer size in kilobytes
                """);
    }

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
            padding: 0;

            height: 100%;
        }
                
        body { 
            color: var(--text-color);
            background-color: var(--background-color); 
        
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            font-size: 16pt;
        
            margin: 0;
            padding: 0;
        
            height: auto;
        }

        """;
}

public class Program {
    static WebApplicationBuilder builder;
    static WebApplication web_app;
    public static void Main(string[] args) {


        Logging.Start();

        int core_count = Environment.ProcessorCount;
        IntPtr mask = (IntPtr)(1L << core_count) - 1;
        Process proc = Process.GetCurrentProcess();
        proc.ProcessorAffinity = mask;

        Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        if (args.Length > 0) {
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "-c") {
                    i++;
                    string path = args[i];
                    Logging.Config($"Using {Path.GetFullPath(path)} as config directory");
                    if (Directory.Exists(Path.GetFullPath(path))) {
                        Directory.SetCurrentDirectory(Path.GetFullPath(path));
                    } else {
                        Logging.Config("Config directory missing. Creating a new one and loading defaults.");
                        Directory.CreateDirectory(Path.GetFullPath(path));
                        Directory.SetCurrentDirectory(Path.GetFullPath(path));
                    }
                }
            }

        } else {
            Logging.Config($"Using {Path.GetFullPath(State.config_dir)} as config directory");

            if (Directory.Exists(Path.GetFullPath(State.config_dir))) {
                Directory.SetCurrentDirectory(Path.GetFullPath(State.config_dir));
            } else {
                Directory.CreateDirectory(Path.GetFullPath(State.config_dir));
                Directory.SetCurrentDirectory(Path.GetFullPath(State.config_dir));
                Logging.Config("Config directory missing. Creating a new one and loading defaults.");
            }
        }

        Logging.Config($"Loading configuration");
        State.InitializeComments();
        State.server = new ConfigWithExpectedValues(State.server_config_values);
        Logging.Config($"Loaded server config");

        SQLitePCL.Batteries.Init();

        ThreadPool.SetMinThreads(16, 16);
        ThreadPool.SetMaxThreads(16, 16);

        MusicDB.Start();

        builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e) { e.Cancel = true; Exit(); };

        var prefixes = State.Prefixes.Trim().Split(' ');

        var port = State.Port;
        var p = prefixes[0];
        if (p.StartsWith("http://")) p = p.Remove(0, 7);
        if (p.StartsWith("https://")) p = p.Remove(0, 8);
        if (p.EndsWith('/')) p = p.Remove(p.Length - 1, 1);
        var prefix = $"http://{p}:{port}";

        builder.WebHost.UseUrls(prefix);

        builder.WebHost.ConfigureKestrel(options => {
            options.Limits.MaxConcurrentConnections = 100;
            options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        });


        web_app = builder.Build();


        web_app.Map("/", async context => {
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(PlayerRenderer.DrawPlayer(), Tasks.cancellation_token);
        });

        web_app.Map("/next/{filename:regex(.*)}", async context => {
            string path = context.Request.RouteValues["filename"]?.ToString();
            path = Uri.UnescapeDataString(path);

            if (string.IsNullOrEmpty(path)) {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Filename parameter is missing");
                return;
            }

            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(MusicDB.FindNextSong(path), Tasks.cancellation_token);
        });

        web_app.Map("/prev/{filename:regex(.*)}", async context => {
            string path = context.Request.RouteValues["filename"]?.ToString();
            path = Uri.UnescapeDataString(path);

            if (string.IsNullOrEmpty(path)) {
                context.Response.StatusCode = 400; 
                await context.Response.WriteAsync("Filename parameter is missing");
                return;
            }

            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(MusicDB.FindPreviousSong(path), Tasks.cancellation_token);
        });

        /*
        web_app.UseStaticFiles(new StaticFileOptions {
            FileProvider = new PhysicalFileProvider(MusicDB.music_root),
            RequestPath = "/get"
        });
        */

        web_app.Map("/get/{filename}", async (HttpContext context, ILogger<Program> logger) => {
            string path = context.Request.RouteValues["filename"]?.ToString();
            path = Uri.UnescapeDataString(path);
            var root = MusicDB.music_root;

            var mime = ConvertAndParse.GetMimeTypeOrOctet(path);
            logger.LogInformation("WUMBA");

            while (root.EndsWith(Path.DirectorySeparatorChar))
                root = root.Remove(root.Length - 1);
            while (path.StartsWith(Path.DirectorySeparatorChar))
                path = path.Remove(0, 1);

            string full_path = root + Path.DirectorySeparatorChar + path;
            if (File.Exists(full_path)) {
                context.Response.ContentType = mime;
                byte[] bytes = await File.ReadAllBytesAsync(full_path);
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);

            } else {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<html><body><h1>404 Not Found</h1><p>File not found.</p></body></html>");
            }
        });

        web_app.Map("list", async context => {
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(base_html_strings_replaced(MusicDB.ListSongsHTML(), "",

                //scripts
                """
                    const music_list = document.getElementById('music-list');                     
                        
                    function scroll_bar_music_list_border() {
                        console.log("ye");
                        console.log(music_list.scrollHeight);
                        console.log(document.documentElement.clientHeight);

                        if (music_list.scrollHeight >= document.documentElement.clientHeight) {
                            music_list.classList.add('scrollbar-visible');
                        } else {
                            music_list.classList.remove('scrollbar-visible');
                        }
                    }
                
                    window.addEventListener('load', () => {
                        scroll_bar_music_list_border();                   
                    });
                    window.addEventListener('resize', scroll_bar_music_list_border);
                
                    function play_song(filename) {     
                        window.parent.play_song(filename);
                    }

                """,

                //styles
                """                       
                body {
                    display: flex;
                    overflow: hidden;
                    flex-wrap: wrap;            
                
                    width: 100vw;            
                    height: 100vh;
                            
                    max-width: 100vw;
                
                    margin: 0;

                    scrollbar-color: var(--main-color) var(--background-color);
                    scrollbar-width: thin;
                }

                :root { 
                    --separator-thickness: 1px;   

                    --track-num-width:60px;
                    --duration-width:30px;

                    --info-margin-width: 10px;
                    --info-margin-count: 6;
                    --info-margin-total-width: calc(var(--info-margin-width) * var(--info-margin-count));

                    --info-width: calc((100vw - var(--track-num-width) - var(--duration-width) - var(--info-margin-total-width) - 2px) / 3);
                }

                iframe { border: none; }                          
                audio-player { display: none; }

                #music-list-container {
                    overflow-y:auto;
                    
                    width:100%;
                    height: 100%;
                    margin: 0;

                    scrollbar-color: var(--main-color) var(--background-color);
                    scrollbar-width: thin;
                }
                
                #music-list {
                    display: flow;
                    width: 100%;
                }  

                .music-list-item {                
                    width:100%;
                    margin: 0;
                }  
                .music-list-item-separator {                
                    width:100%;
                    margin: 0;
                    height: var(--separator-thickness);
                    margin:0;
                    border:none;
                    background-color: var(--secondary-color);
                }  
                
                .item-outer-span { 
                    color: transparent; 
                    text-shadow: 0 0 0 var(--main-color);  
                    width: 100%;
                    max-width:100%;
                    
                    display: flex;

                    align-items: center;
                    white-space: nowrap;     

                    font-size: 14pt;      
                    text-align:left; 
                    height: auto;
                    max-height: auto;
                }

                .item-outer-span:hover { 
                    color: transparent; 
                    background-color: var(--main-color);  
                    text-shadow: 0 0 0 var(--background-color); 
                }
                
                .item-inner-span.track-num {                 
                    flex: 0 0 4ch;
                    margin-left: 0.5rem;
                    margin-right: 0.5rem;

                    overflow: hidden;
                    text-align:left;
                }    

                .item-inner-span { 
                    flex: 1;
                    overflow: hidden;
                    text-align:left;
                    max-width: calc((100% - (4ch + 1rem + 1rem + 5ch + 1rem)) / 3);
                    margin-right: 0.5rem;
                }                

                .item-inner-span.duration { 
                    flex: 0 0 7ch;
                    text-align:right;
                    margin-right: 0.5rem;
               
                }

                a.music-list-item-link {
                    display: flex;
                    flex-grow: 1;
                }

                #music-list.scrollbar-visible {
                    width: calc(100% - 2px) !important;
                    border-right: solid 2px var(--main-color); 
                } 
                """
                ), 
                Tasks.cancellation_token);
        });

        web_app.Map("/base.css", async context => {
            context.Response.ContentType = "text/css";
            await context.Response.WriteAsync(base_css_strings_replaced);
        });


        web_app.Run();
    }

    public static string base_html_strings_replaced(string page_content, string page_title, string script = "", string style = "") {
        var sc_tagged = script;

        if (sc_tagged.Length > 0) {
            sc_tagged = $"<script>{sc_tagged}</script>";
        }

        var st_tagged = style;

        if (st_tagged.Length > 0) {
            st_tagged = $"<style>{st_tagged}</style>";
        }

        return State.base_html.Replace("{page_content}", page_content).Replace("{page_title}", page_title).Replace("{script}", sc_tagged).Replace("{style}", st_tagged);
    }

    static string base_css_strings_replaced {
        get {
            return State.base_css
                .Replace("{main_color}", State.server["theme"]["main_color"].ToColorJSString())
                .Replace("{main_color_dark}", State.server["theme"]["main_color_dark"].ToColorJSString())

                .Replace("{secondary_color}", State.server["theme"]["secondary_color"].ToColorJSString())
                .Replace("{secondary_color_dark}", State.server["theme"]["secondary_color_dark"].ToColorJSString())

                .Replace("{text_color}", State.server["theme"]["text_color"].ToColorJSString())
                .Replace("{background_color}", State.server["theme"]["background_color"].ToColorJSString())
                .Replace("{secondary_background_color}", State.server["theme"]["secondary_background_color"].ToColorJSString())
            ;
        }
    }

    private static void LoadConfig() {
    }
    static void stop_server() {
        Logging.Warning($"Sending cancellation signal to all threads");

        while (Tasks.TaskCount != 0) Thread.Sleep(50);
        Thread.Sleep(500);

        Logging.Message($"All threads stopped");

    }
    static void Exit() {
        Logging.Warning("Shutting down!");

        Logging.Config($"Flushing config");
        State.server.config_file.Flush();

        Logging.Message("Goodbye!");

        Logging.Stop();

        Console.CursorVisible = true;
        Console.ForegroundColor = ConsoleColor.White;
        Console.Title = "";

        System.Environment.Exit(0);
    }
    ~Program() {
        Console.CursorVisible = true;
        Console.ForegroundColor = ConsoleColor.White;
    }
}
