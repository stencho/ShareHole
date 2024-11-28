using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mime;
using HeyRed.Mime;
using ZeroDir.Config;

namespace ZeroDir
{
    public static class CurrentConfig {
        public static ServerConfig server;
        public static FileShareConfig shares;
    }

    public class HttpServer {
        Dictionary<string, string> mime_dict = MIME.get_MIME_dict();
        HttpListener listener;
        
        string page_title = "";
        string page_content = "";

        public void StopServer() {
            run = false;
            listener.Stop();
            for (int i = 0; i < dispatch_threads.Length; i++) {
                Logging.Message($"Stopping thread {i}");
                dispatch_threads[i].Join();
            }
            //while (stopped == false) { }
            //stopped = true;
        }

        //restbool stopped = false;

        string _pd;
        string page_data {
            get { return _pd.Replace("{page_content}", page_content).Replace("{page_title}", page_title); }
            set { _pd = value; }
        }

        bool run = true;

        public async Task ServePageAsync() {

            while (run) {
            }
        }

        const int dispatch_thread_count = 8;
        Thread[] dispatch_threads;

        async void RequestThread() {
            while (listener.IsListening) {
                string url = string.Empty;

                try {
                    // Yeah, this blocks, but that's the whole point of this thread
                    // Note: the number of threads that are dispatching requets in no way limits the number of "open" requests that we can have
                    HttpListenerContext context = await listener.GetContextAsync();

                    // For this demo we only support GET
                    if (context.Request.HttpMethod != "GET") {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                    }

                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;


                    if (request.Url.AbsolutePath.StartsWith("/~/")) {
                        response.Close();
                        continue;
                    }

                    if (request.Url.AbsolutePath == "/favicon.ico") continue;
                    Logging.Message($"REQ {request.Url.AbsolutePath} | {request.HttpMethod} | {request.UserHostName} \n{request.Headers.ToString()} ");
                    string url_path = Uri.UnescapeDataString(request.Url.AbsolutePath);

                    while (url_path.StartsWith('/')) {
                        url_path = url_path.Remove(0, 1);
                    }

                    string share_name = "";
                    var slash_i = url_path.IndexOf('/');
                    if (slash_i > 0) {
                        share_name = url_path.Substring(0, slash_i);
                        if (share_name.EndsWith('/')) share_name = share_name.Remove(share_name.Length - 1, 1);
                    } else {

                    }
                    string folder_path = "";

                    bool show_dirs = true;
                    if (CurrentConfig.shares[share_name].ContainsKey("show_directories")) {
                        show_dirs = CurrentConfig.shares[share_name]["show_directories"].get_bool();
                    }

                    if (CurrentConfig.shares.ContainsKey(share_name)) {
                        folder_path = CurrentConfig.shares[share_name]["path"].ToString();
                        Logging.Message($"WANTS SHARE NAME {share_name} {url_path}");
                    } else {
                        Logging.Error($"Client requested share which doesn't exist: {share_name} {url_path}");
                        response.Close();
                        continue;
                    }
                    
                    url_path = url_path.Remove(0, share_name.Length);
                    string absolute_on_disk_path = folder_path.Replace("\\", "/") + Uri.UnescapeDataString(url_path);
                    byte[] data;

                    response.AddHeader("X-Frame-Options", "DENY");
                    response.AddHeader("Link", "<base_css.css>;rel=stylesheet;media=all");

                    if (request.Url.AbsolutePath.EndsWith("base_css.css")) {
                        absolute_on_disk_path = "base_css.css";
                        Logging.Message(absolute_on_disk_path);
                    }

                    if (Directory.Exists(absolute_on_disk_path)) {
                        try {
                            if (!show_dirs && url_path != "/" ) {
                                Logging.Error($"Attempted to browse outside of share \"{share_name}\" with directories off");
                                page_content = "";
                            } else {
                                page_content = FileListing.BuildListing(folder_path, request.UserHostName, url_path, share_name);
                            }


                            data = Encoding.UTF8.GetBytes(page_data);
                            response.ContentType = "text/html; charset=utf-8";
                            response.ContentEncoding = Encoding.UTF8;
                            response.ContentLength64 = data.LongLength;
                            response.SendChunked = true;


                            var task = response.OutputStream.WriteAsync(data, 0, data.Length);

                            task.GetAwaiter().OnCompleted(() => {
                                response.Close();
                                Logging.Message("Finished write");
                            });

                        } catch (HttpListenerException ex) {
                            Logging.Error(ex.Message);
                            response.Close();
                        }

                    } else if (File.Exists(absolute_on_disk_path)) {
                        try {
                            string mimetype;
                            try {
                                mimetype = MimeTypesMap.GetMimeType(absolute_on_disk_path);
                            } catch {
                                mimetype = "application/octet-stream";
                            }

                            Logging.Message($"Content-type: {mimetype}");
                            response.ContentType = mimetype;

                            if (!show_dirs && url_path.Count(x => x == '/') > 1 ) {
                                Logging.Error($"Attempted to open file outside of share \"{share_name}\" with directories off");
                                continue;
                            }

                            Logging.Message($"file: {absolute_on_disk_path}");
                            FileStream fs = File.OpenRead(absolute_on_disk_path);

                            response.AddHeader("Content-Disposition", "inline");
                            response.AddHeader("Cache-Control", "no-cache");
                            response.AddHeader("X-Frame-Options", "allowall");

                            Logging.Warning($"Content-type: {mimetype}");
                            response.ContentType = mimetype;
                            response.AddHeader("filename", request.Url.AbsolutePath.Remove(0, 1));
                            response.ContentLength64 = fs.Length;
                            response.SendChunked = false;

                            Logging.Message("Starting write");

                            var task = fs.CopyToAsync(context.Response.OutputStream);

                            task.GetAwaiter().OnCompleted(() => {
                                response.Close();
                                Logging.Message("Finished write");
                                fs.Close();
                            });

                        } catch (HttpListenerException ex) {
                            Logging.Error(ex.Message);
                            response.Close();
                        }

                    } else {
                        try {
                            page_content = $"<b>{absolute_on_disk_path} NOT FOUND</b>";
                            data = Encoding.UTF8.GetBytes(page_data);
                            response.ContentType = "text/html; charset=utf-8";
                            response.ContentEncoding = Encoding.UTF8;
                            response.ContentLength64 = data.LongLength;
                            response.AddHeader("X-Frame-Options", "deny");
                            response.SendChunked = true;

                            var task = response.OutputStream.WriteAsync(data, 0, data.Length);

                            task.GetAwaiter().OnCompleted(() => {
                                response.Close();
                                Logging.Message("Finished write");
                            });

                        } catch (HttpListenerException ex) {
                            Logging.Error(ex.Message);
                            response.Close();
                        }
                    }

                } catch (System.Net.HttpListenerException e) {
                    // Bail out - this happens on shutdown
                    return;
                } catch (Exception e) {
                    Console.WriteLine("Unexpected exception: {0}", e.Message);
                }
            }

            Logging.Message($"Stopped thread");
        }


        public void StartServer() {
            page_data = File.ReadAllText("base_page");

            listener = new HttpListener();

            var port = CurrentConfig.server.values["server"]["port"].get_int();

            var prefixes = CurrentConfig.server.values["server"]["prefix"].ToString().Trim().Split(' ');

            for (int i = 0; i < prefixes.Length; i++) {
                string prefix = prefixes[i].Trim();
                if (prefix.StartsWith("http://")) prefix = prefix.Remove(0, 7);
                if (prefix.StartsWith("https://")) prefix = prefix.Remove(0, 8);
                if (prefix.EndsWith('/')) prefix = prefix.Remove(prefix.Length - 1, 1);

                //listener.Prefixes.Add($"http://*{port}/");
                listener.Prefixes.Add($"http://{prefix}:{port}/");
                Logging.Message("Using prefix: " + $"http://{prefix}:{port}/");
            }

            listener.Start();

            dispatch_threads = new Thread[dispatch_thread_count];
            Logging.Message($"Starting server on port {port} with {dispatch_thread_count} threads");

            for (int i = 0; i < dispatch_thread_count; i++) {
                dispatch_threads[i] = new Thread(RequestThread);
                dispatch_threads[i].Start();
                Logging.Message($"Started thread {i}");
            }


            //Task listener_task = ServePage();
            //listener_task.GetAwaiter().GetResult();
            //listener.Close();
            
        }

    }
}