using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mime;
using HeyRed.Mime;
using ZeroDir.Configuration;
using System.ComponentModel.Design;

namespace ZeroDir
{
    public class FolderServer {
        bool running = true;
        HttpListener listener;
        
        string page_title = "";
        string page_content = "";

        string CSS = "";
        public string id { get; private set; }
        public string name { get; private set; }

        int current_sub_thread_count = 0;

        public void StopServer() {
            running = false;

            for (int i = 0; i < dispatch_threads.Length; i++) {
                dispatch_threads[i].Join();
                Logging.ThreadMessage($"Stopped thread", $"{name}:{i}", i);
            }

            while (true) {
                if (all_threads_stopped())
                    break;
            }

            listener.Stop();
        }

        string _pd;
        string page_data {
            get { return _pd.Replace("{page_content}", page_content).Replace("{page_title}", page_title); }
            set { _pd = value; }
        }

        int dispatch_thread_count = 64;
        public Thread[] dispatch_threads;

        public bool all_threads_stopped () {
            int i = 0;

            foreach(Thread t in dispatch_threads) {
                if (t.ThreadState != ThreadState.Stopped) {
                    i++;
                }
            }

            return i + current_sub_thread_count == 0;
        }

        async void RequestThread(object? name_id) {
            (string name, int id) nid = (((string, int))name_id);
            string thread_name = nid.name.ToString();
            int thread_id = nid.id;

            Logging.ThreadMessage($"Started thread", thread_name, thread_id);

            while (listener.IsListening && running) {
                HttpListenerContext context = null;

                try {
                    context = await listener.GetContextAsync();

                } catch(HttpListenerException ex) {
                    //if we're not running, then that means Stop was called, so this error is expected, same with the ObjectDisposedException                    
                    if (running) {
                        Logging.ThreadError($"Failed to get context: {ex.Message}", thread_name, thread_id);
                    } 
                    return;

                } catch (ObjectDisposedException ex) {
                    if (running) {
                        Logging.ThreadError($"Failed to get context: {ex.Message}", thread_name, thread_id);
                    } 
                    return;
                }

                var request = context.Request;

                //Set up response
                context.Response.KeepAlive = false;
                context.Response.ContentEncoding = Encoding.UTF8;
                context.Response.AddHeader("X-Frame-Options", "DENY");
                context.Response.AddHeader("Keep-alive", "false");
                context.Response.AddHeader("Cache-control", "no-store");
                context.Response.AddHeader("Content-Disposition", "inline");
                context.Response.AddHeader("Accept-ranges", "none");
                context.Response.SendChunked = false;

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

                string url_path = Uri.UnescapeDataString(request.Url.AbsolutePath);
                string passdir = CurrentConfig.server["server"]["passdir"].get_string().Trim();

                string share_name = "";
                string folder_path = "";

                //Check if passdir is correct
                if (!url_path.StartsWith($"/{passdir}/") || url_path == ($"/{passdir}/" )) {
                    context.Response.Abort();
                    continue;
                } else {
                    url_path = url_path.Remove(0, 5);
                }

                //Clean URL
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
                if (CurrentConfig.shares.ContainsKey(share_name) && !request.Url.AbsolutePath.EndsWith("base.css")) {
                    //Check if directories should be listed
                    if (CurrentConfig.shares[share_name].ContainsKey("show_directories")) {
                        show_dirs = CurrentConfig.shares[share_name]["show_directories"].get_bool();
                    }

                    folder_path = CurrentConfig.shares[share_name]["path"].ToString();
                    //Logging.Message($"Accessing share: {share_name}");

                } else if (!request.Url.AbsolutePath.EndsWith("base.css")) {
                    Logging.ThreadError($"Client requested share which doesn't exist: {share_name} {url_path}", thread_name, thread_id);
                    context.Response.Abort();
                    continue;
                }                    
                url_path = url_path.Remove(0, share_name.Length);

                string absolute_on_disk_path = folder_path.Replace("\\", "/") + Uri.UnescapeDataString(url_path);
                byte[] data = null;

                //Requested CSS file
                if (request.Url.AbsolutePath.EndsWith("base.css")) {                    
                    absolute_on_disk_path = Path.GetFullPath("base.css");
                    Logging.ThreadMessage($"Requesting base.css", thread_name, thread_id);

                    data = Encoding.UTF8.GetBytes(CSS);
                    context.Response.ContentType = "text/css; charset=utf-8";
                    context.Response.ContentLength64 = data.LongLength;

                    current_sub_thread_count++;
                    context.Response.OutputStream.BeginWrite(data, 0, data.Length, result => {
                        context.Response.OutputStream.EndWrite(result);
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.StatusDescription = "400 OK";
                        context.Response.Close();
                        Logging.ThreadMessage("Sent CSS", thread_name, thread_id);
                        current_sub_thread_count--;
                    }, context.Response);

                //Requested a directory
                } else if (Directory.Exists(absolute_on_disk_path)) {
                    if (!show_dirs && url_path != "/") {
                        Logging.ThreadError($"Attempted to browse outside of share \"{share_name}\" with directories off", thread_name, thread_id);
                        page_content = "";
                    } else {
                        if (CurrentConfig.shares[share_name].ContainsKey("style")) {
                            switch (CurrentConfig.shares[share_name]["style"].get_string()) {
                                case "gallery":
                                    page_content = FileListing.BuildGallery(folder_path, request.UserHostName, url_path, share_name);
                                    data = Encoding.UTF8.GetBytes(CurrentConfig.base_html);
                                    break;
                                default:
                                    page_content = FileListing.BuildListing(folder_path, request.UserHostName, url_path, share_name);
                                    data = Encoding.UTF8.GetBytes(page_data);
                                    break;
                            }
                        } else {
                            page_content = FileListing.BuildListing(folder_path, request.UserHostName, url_path, share_name);
                            data = Encoding.UTF8.GetBytes(page_data);
                        }                    
                    }

                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = data.LongLength;

                    current_sub_thread_count++;
                    context.Response.OutputStream.BeginWrite(data, 0, data.Length, result => {
                        context.Response.OutputStream.EndWrite(result);
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.StatusDescription = "400 OK";
                        context.Response.Close();
                        Logging.ThreadMessage($"Sent directory listing for {url_path}", thread_name, thread_id);
                        current_sub_thread_count--;
                    }, context.Response);

                //Requested a non-CSS file
                } else if (File.Exists(absolute_on_disk_path)) {
                    string mimetype;
                    try {
                        mimetype = MimeTypesMap.GetMimeType(absolute_on_disk_path);
                    } catch {
                        mimetype = "application/octet-stream";
                    }
                    context.Response.ContentType = mimetype;

                    if (!show_dirs && url_path.Count(x => x == '/') > 1) {
                        Logging.ThreadError($"Attempted to open file outside of share \"{share_name}\" with directories off", thread_name, thread_id);
                        context.Response.Abort();
                        continue;
                    }

                    var using_extensions = false;
                    string[] extensions = null;

                    if (CurrentConfig.shares[share_name].ContainsKey("extensions")) {
                        extensions = CurrentConfig.shares[share_name]["extensions"].ToString().Trim().ToLower().Split(" ");
                        using_extensions = true;
                        for (int i = 0; i < extensions.Length; i++) {
                            extensions[i] = extensions[i].Trim();
                            extensions[i] = extensions[i].Replace(".", "");
                        }
                    }

                    if (using_extensions && (Path.HasExtension(absolute_on_disk_path) && !extensions.Contains(Path.GetExtension(absolute_on_disk_path).Replace(".","").ToLower()))) {
                        Logging.ThreadError($"Attempted to open file in \"{share_name}\" with disallowed file extension \"{Path.GetExtension(absolute_on_disk_path).Replace(".", "").ToLower()}\"", thread_name, thread_id);
                        context.Response.Abort();
                        continue;
                    }

                    Logging.ThreadMessage($"[Share] {share_name} [Filename]: {absolute_on_disk_path} [Content-type] {mimetype}", thread_name, thread_id);
                    
                    context.Response.AddHeader("filename", request.Url.AbsolutePath.Remove(0, 1));
                    context.Response.ContentType = mimetype;                        
                    context.Response.SendChunked = false;

                    Logging.ThreadMessage($"Starting write on {url_path}", thread_name, thread_id);

                    FileStream fs = File.OpenRead(absolute_on_disk_path);

                    context.Response.ContentLength64 = fs.Length;

                    current_sub_thread_count++;
                    var task = fs.CopyToAsync(context.Response.OutputStream);

                    task.GetAwaiter().OnCompleted(() => {
                        try {
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.StatusDescription = "400 OK";
                            context.Response.OutputStream.Close();
                            context.Response.Close();
                            Logging.ThreadMessage($"Finished write on {url_path}", thread_name, thread_id);
                            fs.Close();
                        } catch (HttpListenerException ex) {
                            Logging.ThreadError($"{ex.Message}", thread_name, thread_id);
                        }
                        current_sub_thread_count--;
                    });

                //User gave a very fail URL
                } else {
                    page_content = $"<b>NOT FOUND</b>";
                    data = Encoding.UTF8.GetBytes(page_data);
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = data.LongLength;

                    current_sub_thread_count++;
                    context.Response.OutputStream.BeginWrite(data, 0, data.Length, result => {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.StatusDescription = "404 NOT FOUND";
                        context.Response.OutputStream.Close();
                        context.Response.Close();
                        Logging.ThreadMessage("Finished writing 404", thread_name, thread_id);
                        current_sub_thread_count--;
                    }, context.Response);
                }
            }

            Logging.ThreadMessage($"Stopped thread", thread_name, thread_id);
        }


        public void StartServer(string id) {
            this.id = id;

            if (CurrentConfig.use_html_file) {
                if (File.Exists("base.html"))
                    page_data = File.ReadAllText("base.html");
                else {
                    Logging.Error("use_css_file enabled, but base.css is missing from the config directory. Writing default.");
                    page_data = CurrentConfig.base_html;
                    File.WriteAllText("base.html", page_data);                    
                }                    
            } else {
                page_data = CurrentConfig.base_html;
            }

            if (CurrentConfig.use_css_file) {
                if (File.Exists("base.css")) {
                    CSS = File.ReadAllText("base.css");
                } else {
                    Logging.Error("use_css_file enabled, but base.css is missing from the config directory. Writing default.");
                    CSS = CurrentConfig.base_css;
                    File.WriteAllText("base.css", CSS);               
                }
            } else { 
                CSS = CurrentConfig.base_css;
            }


            listener = new HttpListener();

            var port = CurrentConfig.server["server"]["port"].get_int();
            var prefixes = CurrentConfig.server["server"]["prefix"].ToString().Trim().Split(' ');
            dispatch_thread_count = CurrentConfig.server["server"]["threads"].get_int();

            var p = prefixes[0];
            if (p.StartsWith("http://")) p = p.Remove(0, 7);
            if (p.StartsWith("https://")) p = p.Remove(0, 8);
            if (p.EndsWith('/')) p = p.Remove(p.Length - 1, 1);
            name = $"{p}:{port}";

            for (int i = 0; i < prefixes.Length; i++) {
                string prefix = prefixes[i].Trim();
                
                if (prefix.StartsWith("http://")) prefix = prefix.Remove(0, 7);
                if (prefix.StartsWith("https://")) prefix = prefix.Remove(0, 8);
                if (prefix.EndsWith('/')) prefix = prefix.Remove(prefix.Length - 1, 1);

                listener.Prefixes.Add($"http://{prefix}:{port}/");
                Logging.Message("Using prefix: " + $"http://{prefix}:{port}/");
            }

            listener.Start();

            dispatch_threads = new Thread[dispatch_thread_count];
            Logging.Message($"Starting server on port {port}");

            while (listener.IsListening) {
                for (int i = 0; i < dispatch_thread_count; i++) {
                    if (dispatch_threads[i] == null) {
                        dispatch_threads[i] = new Thread(RequestThread);
                        dispatch_threads[i].Name = $"{prefixes[0]}:{port}:{i}";
                        dispatch_threads[i].Start((dispatch_threads[i].Name, i));
                    }
                }
            }
        }
    }
}