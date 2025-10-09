using System.Net;
using System.Text;
using ImageMagick;
using FFMpegCore;
using TagLib;
using File = System.IO.File;

namespace ShareHole {
    public class ShareServer {
        bool running = true;

        HttpListener listener;

        string CSS = "";
        public string prefix { get; private set; }

        static string base_page_content = "";

        public static string page_content_strings_replaced(string page_content, string page_title, string script = "", string style = "") {
            var sc_tagged = script;

            if (sc_tagged.Length > 0) {
                sc_tagged = $"<script>{sc_tagged}</script>";
            }

            var st_tagged = style;

            if (st_tagged.Length > 0) {
                st_tagged = $"<style>{st_tagged}</style>";
            }

            return base_page_content.Replace("{page_content}", page_content).Replace("{page_title}", page_title).Replace("{script}", sc_tagged).Replace("{style}", st_tagged);
        }

        static string base_css_data_replaced {
            get {
                return State.base_css
                    .Replace("{thumbnail_size}", State.server["gallery"]["thumbnail_size"].ToInt().ToString())

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

        public async void Start() {
            if (State.use_html_file) {
                if (File.Exists("base.html"))
                    base_page_content = File.ReadAllText("base.html");
                else {
                    Logging.Error("use_css_file enabled, but base.css is missing from the config directory. Writing default.");
                    base_page_content = State.base_html;
                    File.WriteAllText("base.html", base_page_content);
                }
            } else {
                base_page_content = State.base_html;
            }

            if (State.use_css_file) {
                if (File.Exists("base.css")) {
                    CSS = File.ReadAllText("base.css");
                } else {
                    Logging.Error("use_css_file enabled, but base.css is missing from the config directory. Writing default.");
                    CSS = base_css_data_replaced;
                    File.WriteAllText("base.css", CSS);
                }
            } else {
                CSS = base_css_data_replaced;
            }


            listener = new HttpListener();


            var port = State.Port;
            var prefixes = State.Prefixes.Trim().Split(' ');
            
            var p = prefixes[0];
            if (p.StartsWith("http://")) p = p.Remove(0, 7);
            if (p.StartsWith("https://")) p = p.Remove(0, 8);
            if (p.EndsWith('/')) p = p.Remove(p.Length - 1, 1);
            prefix = $"{p}:{port}";

            for (int i = 0; i < prefixes.Length; i++) {
                string prefix = prefixes[i].Trim();

                if (prefix.StartsWith("http://")) prefix = prefix.Remove(0, 7);
                if (prefix.StartsWith("https://")) prefix = prefix.Remove(0, 8);
                if (prefix.EndsWith('/')) prefix = prefix.Remove(prefix.Length - 1, 1);

                listener.Prefixes.Add($"http://{prefix}:{port}/");
                Logging.Message("Using prefix: " + $"http://{prefix}:{port}/");
            }

            listener.Start();

            Logging.Message($"Starting server on port {port}");
            Parallel.For(0, State.RequestThreads, i => {
                Task.Run(() => { HandleRequest($"{prefixes[0]}:{port}:{i}", i); });
            });

            Logging.Message($"Started {State.RequestThreads} request getter threads");
        }

        public async void StopListener() {
            listener.Stop();
        }

        enum command_dirs {
            none,
            thumbnail,
            to_jpg,
            to_png,
            random_images,
            transcode,
            file_list,
            music_player_dir,
            music_info,
            next_track,
            previous_track
        }

        internal CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
        internal CancellationToken cancellation_token => cancellation_token_source.Token;
        internal int running_request_getter_threads = 0;

        
        private void handle_random_images(int count, string thread_name, int thread_id, bool dir_exists, ref string page_content, HttpListenerContext context, HttpListenerRequest request, string absolute_on_disk_path, string passdir, string share_name, string url_path) {
            if (!dir_exists) {
                page_content = $"<p class=\"head\"><color=white><b>NOT A VALID DIRECTORY</b></p>";
                Send.ErrorBadRequest(page_content, context);
            }

            Logging.ThreadMessage($"Building random image array for {url_path}", thread_name, thread_id);
            
            DirectoryInfo di = new DirectoryInfo(absolute_on_disk_path);

            context.Response.ContentType = "text/html; charset=utf-8";

            FileInfo[] files;
            files = di.GetFiles("*", SearchOption.TopDirectoryOnly);
            
            int files_pulled = 0;
            int files_to_send = count;
            int attempts = 0;
            int max_attempts = files_to_send * 5;
            
            string[] files_for_sending = new string[files_to_send];

            string current_file = "";
            Random rng = new Random();
            
            current_file += $"<center><div style=\"overflow-y: hidden; display:inline; align-items: center;  vertical-align: middle; \">";
            
            while (files_pulled < files_to_send && attempts <= max_attempts) {
                var f = files[rng.Next(files.Length)];
                var f_mime = ConvertAndParse.GetMimeTypeOrOctet(f.FullName);
                
                if (ConvertAndParse.IsValidImage(f_mime)) {
                    //current_file += "<div style=\"height: 128px; display: flex;\">";
                    current_file += $"<img style=\"max-width: 128px; max-height: 128px;\" src=\"http://{request.UserHostName}/{passdir}/{share_name}{url_path}{f.Name}\">";
                    //current_file += $"</div>";        
                    files_pulled++;
                }

                attempts++;
            }
            current_file += $"</div></center>";
            
            var ridata = Encoding.UTF8.GetBytes(current_file);
            context.Response.ContentLength64 = ridata.Length;
            
            try {
                State.StartTask(async () => {
                    using (MemoryStream ms = new MemoryStream(ridata, false)) {
                        await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                            Logging.ThreadMessage($"Sent random images from {url_path}", thread_name, thread_id);
                            Send.OK(context);
                        }, State.cancellation_token);
                    }
                });
                
            } catch (HttpListenerException ex) {
                Logging.ThreadError($"Exception: {ex.Message}", thread_name, thread_id);
                page_content = $"<p class=\"head\"><color=white><b>NOT A VALID DIRECTORY</b></p>";
                Send.ErrorBadRequest(page_content, context);
            }
            
        }
        

        async void HandleRequest(string thread_name, int thread_id) {

            if (cancellation_token.IsCancellationRequested) {
                Interlocked.Decrement(ref running_request_getter_threads);
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Stopping thread", thread_name, thread_id);
                return;
            }

            if (State.LogLevel == Logging.LogLevel.ALL)
                Logging.ThreadMessage($"Started thread", thread_name, thread_id);
            Interlocked.Increment(ref running_request_getter_threads);


            while (listener.IsListening && running) {
                HttpListenerContext context = null;

                try {
                    //Asynchronously begin waiting for a new HTTP request,
                    //but continue on to the while loop below to make it
                    //possible to exit idly waiting threads 
                    listener.GetContextAsync().ContinueWith(t => { context = t.Result; }, cancellation_token);

                    while (context == null) {
                        if (cancellation_token.IsCancellationRequested) {
                            Interlocked.Decrement(ref running_request_getter_threads);
                            if (State.LogLevel == Logging.LogLevel.ALL)
                                Logging.ThreadMessage($"Stopping thread", thread_name, thread_id);
                            return;
                        }

                        Thread.Sleep(5);
                    }

                } catch (HttpListenerException ex) {
                    //if we're not running, then that means Stop was called, so this error is expected, same with the ObjectDisposedException                    
                    if (running) {
                        Logging.ThreadError($"Failed to get context: {ex.Message}", thread_name, thread_id);
                    }
                    continue;

                } catch (ObjectDisposedException ex) {
                    if (running) {
                        Logging.ThreadError($"Failed to get context: {ex.Message}", thread_name, thread_id);
                    }
                    continue;
                } catch (TaskCanceledException ex) {
                    Interlocked.Decrement(ref running_request_getter_threads);
                    if (State.LogLevel == Logging.LogLevel.ALL)
                        Logging.ThreadMessage($"Stopping thread", thread_name, thread_id);
                    return;
                }

                /* COMICAL AMOUNTS OF SETUP */
                var request = context.Request;

                if (request.ProtocolVersion.Major == 1 && request.ProtocolVersion.Minor == 0) {
                    Logging.ThreadWarning($"Client attempted to connect using HTTP/1.0 from [{request.UserHostAddress}]", thread_name, thread_id);
                    context.Response.StatusCode = (int)HttpStatusCode.HttpVersionNotSupported;
                    context.Response.Close();
                    continue;
                }

                //Set up response
                context.Response.KeepAlive = false;
                context.Response.ContentEncoding = Encoding.UTF8;
                context.Response.AddHeader("X-Frame-Options", "SAMEORIGIN");
                //context.Response.AddHeader("Keep-alive", "false");
                context.Response.AddHeader("Cache-control", "no-cache");
                context.Response.AddHeader("Content-Disposition", "inline");
                context.Response.AddHeader("Accept-Ranges", "bytes");
                context.Response.SendChunked = true;

                //only support GET
                if (context.Request.HttpMethod != "GET") {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    continue;
                }

                //No current favicon support
                if (request.Url.AbsolutePath == "/favicon.ico") {
                    context.Response.Abort();
                    continue;
                }

                string page_content = "";

                string url_path = Uri.UnescapeDataString(request.Url.AbsolutePath);
                string passdir = State.server["server"]["passdir"].ToString().Trim();

                string share_name = "";
                string folder_path = "";

                //Check if passdir is correct
                if (!url_path.StartsWith($"/{passdir}/") || url_path == ($"/{passdir}/")) {
                    context.Response.Abort();
                    continue;
                } else {
                    url_path = url_path.Remove(0, passdir.Length + 1);
                }

                //check for command directory
                command_dirs command_dir = command_dirs.none;
                foreach (var v in Enum.GetValues(typeof(command_dirs))) {
                    var vs = v.ToString().Trim();
                    if (url_path.ToLower().StartsWith($"/{vs.ToLower()}/")) {
                        url_path = url_path.Remove(0, $"/{vs}/".Length);
                        command_dir = Enum.Parse<command_dirs>(vs);
                    }
                }

                //Clean URL
                while (url_path.EndsWith("/#")) url_path = url_path.Remove(url_path.Length - 2);
                while (url_path.StartsWith('/')) {
                    url_path = url_path.Remove(0, 1);
                }

                //Extract share name from start of URL
                var slash_i = url_path.IndexOf('/');
                if (slash_i > 0) {
                    share_name = url_path.Substring(0, slash_i);
                    if (share_name.EndsWith('/')) share_name = share_name.Remove(share_name.Length - 1, 1);

                } else if (!request.Url.AbsolutePath.EndsWith("base.css")) {
                    //if the user types, for example, localhost:8080/loot/share instead of /loot/share/
                    //redirect to /loot/share/ so that the rest of this garbage works
                    share_name = url_path;
                    url_path += "/";
                    context.Response.Redirect(url_path);
                    Logging.ThreadWarning($"Share recognized, missing trailing slash, redirecting to {url_path}", thread_name, thread_id);
                }

                bool show_dirs = true;

                //if requested share exists
                if (State.shares.ContainsKey(share_name) && !request.Url.AbsolutePath.EndsWith("base.css")) {
                    //Check if directories should be listed
                    if (State.shares[share_name].ContainsKey("show_directories")) {
                        show_dirs = State.shares[share_name]["show_directories"].ToBool();
                    }

                    folder_path = State.shares[share_name]["path"].ToString();
                    //Logging.Message($"Accessing share: {share_name}");

                } else if (!request.Url.AbsolutePath.EndsWith("base.css")) {
                    Logging.ThreadError($"Client requested share which doesn't exist: {share_name} {url_path}", thread_name, thread_id);
                    context.Response.Abort();
                    continue;
                }
                url_path = url_path.Remove(0, share_name.Length);

                string absolute_on_disk_path = folder_path.Replace("\\", "/") + Uri.UnescapeDataString(url_path);

                var ext = new FileInfo(absolute_on_disk_path).Extension.Replace(".", "");
                var mime = ConvertAndParse.GetMimeTypeOrOctet(absolute_on_disk_path);

                bool file_exists = File.Exists(absolute_on_disk_path);
                bool dir_exists = Directory.Exists(absolute_on_disk_path);

                /* ACTUAL SERVING BEGINS HERE */

                /* CLIENT REQUESTED CSS */
                if (request.Url.AbsolutePath.EndsWith("base.css")) {
                    absolute_on_disk_path = Path.GetFullPath("base.css");
                    Logging.ThreadMessage($"Requested base.css", thread_name, thread_id);

                    var data = Encoding.UTF8.GetBytes(CSS);
                    context.Response.ContentType = "text/css; charset=utf-8";
                    context.Response.ContentLength64 = data.LongLength;
                    State.StartTask(async () => {
                        using (MemoryStream ms = new MemoryStream(data, false)) {
                            await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                //ok_close(context);
                            }, State.cancellation_token);
                        }
                    });


                    /* COMMAND DIRECTORIES */
                } else if (command_dir != command_dirs.none) {

                    DirectoryInfo di;
                    FileInfo[] files;
                    
                    switch (command_dir) {
                        case command_dirs.none: break;
                        case command_dirs.thumbnail: // REQUESTED THUMBNAIL

                            if (file_exists && (mime.StartsWith("video") || ConvertAndParse.IsValidImage(mime))) {
                                enable_cache(context);
                                State.StartTask(() => { ThumbnailManager.BuildThumbnail(new FileInfo(absolute_on_disk_path), context, this, mime, thread_id); });

                            } else {
                                page_content = $"<p class=\"head\"><color=white><b>NOT AN IMAGE, VIDEO OR POSTSCRIPT FILE</b></p>";
                                Send.ErrorBadRequest(page_content, context);
                            }

                            break;

                        case command_dirs.to_jpg: // REQUESTED IMAGE -> JPG CONVERSION

                            if (file_exists && ConvertAndParse.IsValidImage(mime)) {
                                enable_cache(context);

                                using (MagickImage mi = new MagickImage(absolute_on_disk_path)) {
                                    if (mi.Orientation != OrientationType.Undefined)
                                        mi.AutoOrient();

                                    mi.Settings.Format = MagickFormat.Jpg;

                                    var compress = State.server["conversion"]["jpeg_compression"].ToBool();
                                    var quality = State.server["conversion"]["jpeg_quality"].ToInt();

                                    if (quality < 0) quality = 0;
                                    if (quality > 100) quality = 100;

                                    if (compress) {
                                        mi.Settings.Compression = CompressionMethod.JPEG;
                                        mi.Quality = (uint)quality;
                                    } else {
                                        mi.Settings.Compression = CompressionMethod.LosslessJPEG;
                                        mi.Quality = 100;
                                    }

                                    context.Response.ContentType = "image/jpeg";

                                    var bytes = mi.ToByteArray();
                                    context.Response.ContentLength64 = bytes.Length;
                                    State.StartTask(async () => {
                                        using (MemoryStream ms = new MemoryStream(bytes, false)) {
                                            await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                                Send.OK(context);
                                            }, State.cancellation_token);
                                        }
                                    });
                                }

                            } else {
                                page_content = $"<p class=\"head\"><color=white><b>NOT AN IMAGE FILE</b></p>";
                                Send.ErrorBadRequest(page_content, context);
                            }
                            break;

                        case command_dirs.to_png: // REQUESTED IMAGE -> PNG CONVERSION

                            if (file_exists && ConvertAndParse.IsValidImage(mime)) {
                                enable_cache(context);
                                MagickReadSettings settings = null;

                                var vector = mime == "application/pdf" || mime == "application/postscript";

                                if (vector) settings = new MagickReadSettings { Density = new Density(300) };

                                using (MagickImage mi = new MagickImage(absolute_on_disk_path, settings)) {

                                    if (mi.Orientation != OrientationType.Undefined)
                                        mi.AutoOrient();

                                    mi.Settings.Format = MagickFormat.Png;

                                    context.Response.ContentType = "image/png";

                                    var bytes = mi.ToByteArray();
                                    context.Response.ContentLength64 = bytes.Length;

                                    State.StartTask(async () => {
                                        using (MemoryStream ms = new MemoryStream(bytes, false)) {
                                            await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                                Send.OK(context);
                                            }, State.cancellation_token);
                                        }
                                    });

                                }

                            } else {
                                page_content = $"<p class=\"head\"><color=white><b>NOT AN IMAGE FILE</b></p>";
                                Send.ErrorBadRequest(page_content, context);
                            }
                            break;

                        case command_dirs.random_images:
                            handle_random_images(15, thread_name, thread_id, dir_exists, ref page_content, context, request, absolute_on_disk_path, passdir, share_name, url_path);
                            break;
                        
                        case command_dirs.transcode: // REQUESTED MP4 TRANSCODE STREAM

                            if (file_exists && mime.StartsWith("video")) {
                                State.StartTask(async () => { await Transcoding.StreamVideoAsMp4(new FileInfo(absolute_on_disk_path), context); });

                            } else if (file_exists && mime.StartsWith("audio")) {
                                page_content = $"<p class=\"head\"><color=white><b>NOT IMPLEMENTED</b></p>";
                                Send.ErrorBadRequest(page_content, context);

                            } else {
                                page_content = $"<p class=\"head\"><color=white><b>NOT A VIDEO FILE</b></p>";
                                Send.ErrorBadRequest(page_content, context);
                            }
                            break;
                        
                        case command_dirs.file_list: // REQUESTED PLAINTEXT OF FILES IN DIRECTORY
                            
                            if (!dir_exists) {
                                page_content = $"<p class=\"head\"><color=white><b>NOT A VALID DIRECTORY</b></p>";
                                Send.ErrorBadRequest(page_content, context);
                            }

                            di = new DirectoryInfo(absolute_on_disk_path);
                            context.Response.ContentType = "text/plain; charset=utf-8";
                            files = di.GetFiles();
                            
                            string raw_file_list = "";

                            foreach (FileInfo fi in files) {
                                raw_file_list += $"http://{request.UserHostName}/{passdir}/{share_name}{url_path}/{fi.Name}\n";
                            }

                            var data = Encoding.UTF8.GetBytes(raw_file_list);
                            context.Response.ContentLength64 = data.Length;
                            
                            try {
                                State.StartTask(async () => {
                                    using (MemoryStream ms = new MemoryStream(data, false)) {
                                        await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                            Logging.ThreadMessage($"Sent file list for {url_path}", thread_name, thread_id);
                                            Send.OK(context);
                                        }, State.cancellation_token);
                                    }
                                });
                            } catch (HttpListenerException ex) {
                                Logging.ThreadError($"Exception: {ex.Message}", thread_name, thread_id);
                                page_content = $"<p class=\"head\"><color=white><b>NOT A VALID DIRECTORY</b></p>";
                                Send.ErrorBadRequest(page_content, context);
                            }
                            break;
                        case command_dirs.music_player_dir: // REQUESTED MUSIC PLAYER DIRECTORY BROWSER

                            if (!dir_exists) {
                                page_content = $"<p class=\"head\"><color=white><b>NOT A VALID DIRECTORY</b></p>";
                                Send.ErrorBadRequest(page_content, context);
                            }

                            context.Response.ContentType = "text/html; charset=utf-8";

                            page_content = Renderer.MusicPlayerDirectoryView(folder_path, request.UserHostName, url_path, share_name);

                            var script = """
                                const music_list = document.getElementById('music-list'); 
                                
                                function play_song(filename) {     
                                    window.parent.load_song_and_folder(filename);
                                }
                            
                                function change_directory(url) {
                                    window.parent.change_directory(url);                                    
                                }
                                    
                                function scroll_bar_music_list_border() {
                                    if (music_list.scrollHeight > document.documentElement.clientHeight) {
                                        music_list.classList.add('scrollbar-visible');
                                    } else {
                                        music_list.classList.remove('scrollbar-visible');
                                    }
                                }

                                window.addEventListener('load', () => {
                                    scroll_bar_music_list_border();                            
                                    window.parent.change_directory_visual('{url}');                       
                                });

                                window.addEventListener('resize', scroll_bar_music_list_border);
                            """.Replace("{url}", Uri.EscapeDataString(url_path));

                            var data_mpd = Encoding.UTF8.GetBytes(page_content_strings_replaced(page_content, "", script, ""));

                            try {
                                State.StartTask(async () => {
                                    using (MemoryStream ms = new MemoryStream(data_mpd, false)) {
                                        await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                            Logging.ThreadMessage($"Sent directory listing for {url_path}", thread_name, thread_id);
                                            Send.OK(context);
                                        }, State.cancellation_token);
                                    }
                                });

                            } catch (HttpListenerException ex) {
                                Logging.ThreadError($"Exception: {ex.Message}", thread_name, thread_id);
                                page_content = $"<b>NOT AN AUDIO FILE</b>";
                                Send.ErrorBadRequest(page_content, context);
                            }
                            break;

                        case command_dirs.music_info:
                            if (file_exists && mime.StartsWith("audio")) {
                                context.Response.ContentType = "text/html; charset=utf-8";

                                TagLib.File f = TagLib.File.Create(absolute_on_disk_path);
                                
                                var title = f.Tag.Title;
                                var artist = string.Join(", ", f.Tag.Performers);
                                var album = f.Tag.Album;
                                
                                page_content += "<div id=\"audio-info-inner\">";
                                
                                if (!string.IsNullOrEmpty(title))
                                    page_content += $"<div style=\"vertical-align: middle;\" id=\"music-info-title\">{title}</div>";
                                if (!string.IsNullOrEmpty(artist))
                                    page_content += $"<div style=\"vertical-align: middle;\" id=\"music-info-artist\">{artist}</div>";
                                if (!string.IsNullOrEmpty(album))
                                    page_content += $"<div style=\"vertical-align: middle;\" id=\"music-info-album\">{album}</div>";
                                
                                page_content += "</div>";
                                
                                var data_mi = Encoding.UTF8.GetBytes(page_content_strings_replaced(page_content, "", "", ""));

                                try {
                                    State.StartTask(async () => {
                                        using (MemoryStream ms = new MemoryStream(data_mi, false)) {
                                            await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                                Logging.ThreadMessage($"Requested music info for {url_path}", thread_name, thread_id);
                                                Send.OK(context);
                                            }, State.cancellation_token);
                                        }
                                    });

                                } catch (HttpListenerException ex) {
                                    Logging.ThreadError($"Exception: {ex.Message}", thread_name, thread_id);
                                    page_content = $"<b>NOT AN AUDIO FILE</b>";
                                    Send.ErrorBadRequest(page_content, context);
                                }
                            } else {
                                Logging.ThreadError($"Exception", thread_name, thread_id);
                                page_content = $"<b>NOT AN AUDIO FILE</b>";
                                Send.ErrorBadRequest(page_content, context);
                            }
                            break;

                        case command_dirs.previous_track:
                            if (file_exists && mime.StartsWith("audio")) {
                                context.Response.ContentType = "text/text; charset=utf-8";

                                var url_path_no_file = Path.GetDirectoryName(url_path);
                                
                                //get files in containing path
                                FileInfo fi = new FileInfo(absolute_on_disk_path);
                                Logging.ThreadMessage($"{absolute_on_disk_path}", thread_name, thread_id);
                                
                                FileInfo[] files_in_dir = fi.Directory.GetFiles();
                                Logging.ThreadMessage($"{fi.Directory}", thread_name, thread_id);
                                var ordered = files_in_dir.OrderBy(a => a.Name).ToArray();
                                int index = -1;
                                
                                bool NextOut = false;
                                for (int i = ordered.Length - 1; i >= 0; i--) {
                                    if (NextOut) {
                                        var m = ConvertAndParse.GetMimeTypeOrOctet(ordered[i].FullName);
                                        
                                        if (m.StartsWith("audio") && ordered[i] != fi) {
                                            index = i;
                                            break;
                                        } 
                                    }
                                    
                                    if (!NextOut && ordered[i].FullName == fi.FullName) { NextOut = true; }
                                }
                                
                                if (index == -1) {
                                    for (int i = ordered.Length - 1; i >= 0; i--) {
                                        var m = ConvertAndParse.GetMimeTypeOrOctet(ordered[i].FullName);
                                        if (m.StartsWith("audio") && ordered[i] != fi) {
                                            index = i;
                                            break;
                                            
                                        }
                                    }
                                }

                                if (index > -1) 
                                    page_content = $"{share_name}{url_path_no_file}/{ordered[index].Name}";
                                

                                var data_mi = Encoding.UTF8.GetBytes(page_content);

                                try {
                                    State.StartTask(async () => {
                                        using (MemoryStream ms = new MemoryStream(data_mi, false)) {
                                            await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                                Logging.ThreadMessage($"Requested track before {url_path}", thread_name, thread_id);
                                                Logging.ThreadMessage($"-> {page_content}", thread_name, thread_id);
                                                Send.OK(context);
                                            }, State.cancellation_token);
                                        }
                                    });

                                } catch (HttpListenerException ex) {
                                    Logging.ThreadError($"Exception: {ex.Message}", thread_name, thread_id);
                                    page_content = $"<b>NOT AN AUDIO FILE</b>";
                                    Send.ErrorBadRequest(page_content, context);
                                }
                            } else {
                                Logging.ThreadError($"Exception", thread_name, thread_id);
                                page_content = $"<b>NOT AN AUDIO FILE</b>";
                                Send.ErrorBadRequest(page_content, context);
                            }
                            break;
                    
                        case command_dirs.next_track:
                            if (file_exists && mime.StartsWith("audio")) {
                                context.Response.ContentType = "text/text; charset=utf-8";

                                var url_path_no_file = Path.GetDirectoryName(url_path);
                                //get files in containing path
                                FileInfo fi = new FileInfo(absolute_on_disk_path);
                                
                                FileInfo[] files_in_dir = fi.Directory.GetFiles();
                                var ordered = files_in_dir.OrderBy(a => a.Name).ToArray();
                                int index = -1;
                                
                                bool NextOut = false;
                                for (int i = 0; i < ordered.Length; i++) {
                                    if (NextOut) {
                                        var m = ConvertAndParse.GetMimeTypeOrOctet(ordered[i].FullName);
                                        
                                        if (m.StartsWith("audio") && ordered[i] != fi) {
                                            index = i;
                                            break;
                                            
                                        }
                                    }
                                    
                                    if (!NextOut && ordered[i].FullName == fi.FullName) { NextOut = true; }
                                }

                                if (index == -1) {
                                    for (int i = 0; i < ordered.Length; i++) {
                                        var m = ConvertAndParse.GetMimeTypeOrOctet(ordered[i].FullName);
                                        if (m.StartsWith("audio")) {
                                            index = i;
                                            break;
                                        }
                                    }
                                }

                                if (index > -1) 
                                    page_content = $"{share_name}{url_path_no_file}/{ordered[index].Name}";
                                
                                var data_mi = Encoding.UTF8.GetBytes(page_content);

                                try {
                                    State.StartTask(async () => {
                                        using (MemoryStream ms = new MemoryStream(data_mi, false)) {
                                            await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                                Logging.ThreadMessage($"Requested track following {url_path}", thread_name, thread_id);
                                                Logging.ThreadMessage($"s-> {share_name}{url_path_no_file}/{ordered[index].Name}", thread_name, thread_id);
                                                Send.OK(context);
                                            }, State.cancellation_token);
                                        }
                                    });

                                } catch (HttpListenerException ex) {
                                    Logging.ThreadError($"Exception: {ex.Message}", thread_name, thread_id);
                                    page_content = $"<b>NOT AN AUDIO FILE</b>";
                                    Send.ErrorBadRequest(page_content, context);
                                }
                            } else {
                                Logging.ThreadError($"Exception", thread_name, thread_id);
                                page_content = $"<b>NOT AN AUDIO FILE</b>";
                                Send.ErrorBadRequest(page_content, context);
                            }
                            break;
                    }

                    /* REGULAR REQUESTS FOR FILES AND DIRECTORIES */
                } else if (dir_exists) { // REQUESTED DIRECTORY
                    byte[] data = null;

                    if (!show_dirs && url_path != "/") {
                        Logging.ThreadError($"Attempted to browse outside of share \"{share_name}\" with directories off", thread_name, thread_id);
                        page_content = "";
                        context.Response.Abort();
                        continue;
                    } else {
                        //Get the page content based on the share's chosen render style
                        if (State.shares[share_name].ContainsKey("style")) {
                            switch (State.shares[share_name]["style"].ToString()) {
                                case "gallery":
                                    page_content = Renderer.Gallery(folder_path, request.UserHostName, url_path, share_name);
                                    data = Encoding.UTF8.GetBytes(page_content_strings_replaced(page_content, ""));
                                    break;
                                case "music":
                                    page_content = Renderer.MusicPlayerContent(folder_path, request.UserHostName, url_path, share_name);
                                    data = Encoding.UTF8.GetBytes(page_content_strings_replaced(page_content, ""));
                                    break;
                                default:
                                    page_content = Renderer.FileListing(folder_path, request.UserHostName, url_path, share_name);
                                    data = Encoding.UTF8.GetBytes(page_content_strings_replaced(page_content, ""));
                                    break;
                            }
                        } else {
                            //There isn't a render style given in the config, so just use the regular list style
                            page_content = Renderer.FileListing(folder_path, request.UserHostName, url_path, share_name);
                            data = Encoding.UTF8.GetBytes(page_content_strings_replaced(page_content, ""));
                        }
                    }

                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = data.LongLength;

                    try {
                        State.StartTask(async () => {
                            using (MemoryStream ms = new MemoryStream(data, false)) {
                                await ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                                    Send.OK(context);
                                    Logging.ThreadMessage($"Sent directory listing for {url_path}", thread_name, thread_id);
                                }, State.cancellation_token);
                            }
                        });

                    } catch (HttpListenerException ex) {
                        Logging.ThreadError($"Exception: {ex.Message}", thread_name, thread_id);
                    }


                } else if (file_exists) { // REQUESTED FILE
                    string mimetype = ConvertAndParse.GetMimeTypeOrOctet(absolute_on_disk_path);

                    try {
                        if (mimetype.StartsWith("video")) {
                            var anal = FFProbe.Analyse(absolute_on_disk_path);
                            context.Response.AddHeader("X-Content-Duration", ((int)(anal.Duration.TotalSeconds) + 1).ToString());
                        }
                    } catch (Exception ex) { }

                    if (!show_dirs && url_path.Count(x => x == '/') > 1) {
                        Logging.ThreadError($"Attempted to open file outside of share \"{share_name}\" with directories off", thread_name, thread_id);
                        context.Response.Abort();
                        continue;
                    }

                    var using_extensions = false;
                    string[] extensions = null;

                    if (State.shares[share_name].ContainsKey("extensions")) {
                        extensions = State.shares[share_name]["extensions"].ToString().Trim().ToLower().Split(" ");
                        using_extensions = true;
                        for (int i = 0; i < extensions.Length; i++) {
                            extensions[i] = extensions[i].Trim();
                            extensions[i] = extensions[i].Replace(".", "");
                        }
                    }

                    if (using_extensions && (Path.HasExtension(absolute_on_disk_path) && !extensions.Contains(Path.GetExtension(absolute_on_disk_path).Replace(".", "").ToLower()))) {
                        Logging.ThreadError($"Attempted to open file in \"{share_name}\" with disallowed file extension \"{Path.GetExtension(absolute_on_disk_path).Replace(".", "").ToLower()}\"", thread_name, thread_id);
                        context.Response.Abort();
                        continue;
                    }

                    if (State.LogLevel == Logging.LogLevel.ALL)
                        Logging.ThreadMessage($"[Share] {share_name} [Filename]: {absolute_on_disk_path} [Content-type] {mimetype}", thread_name, thread_id);

                    enable_cache(context);
                    context.Response.AddHeader("filename", request.Url.AbsolutePath.Remove(0, 1));
                    context.Response.ContentType = mimetype;

                    Send.FileWithRanges(absolute_on_disk_path, mimetype, context);

                } else { // USER GAVE A FAIL URL
                    page_content = $"<b>NOT FOUND</b>";
                    Send.Error404(page_content, context);
                }
            }

        }

        void enable_cache(HttpListenerContext context) {
            context.Response.Headers.Remove("Cache-control");
            context.Response.AddHeader("Cache-control", "max-age=86400, public");
        }
    }
}