using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mime;
using HeyRed.Mime;
using ZeroDir.Config;
using System.ComponentModel.Design;

namespace ZeroDir
{
    public static class CurrentConfig {
        public static ServerConfig server;
        public static FileShareConfig shares;
    }

    public class FolderServer {
        Dictionary<string, string> mime_dict = MIME.get_MIME_dict();
        HttpListener listener;
        
        string page_title = "";
        string page_content = "";

        public void StopServer() {
            listener.Stop();

            for (int i = 0; i < dispatch_threads.Length; i++) {
                Logging.Message($"Stopping thread {i}");
                dispatch_threads[i].Join();
            }
        }

        string _pd;
        string page_data {
            get { return _pd.Replace("{page_content}", page_content).Replace("{page_title}", page_title); }
            set { _pd = value; }
        }

        int dispatch_thread_count = 64;
        Thread[] dispatch_threads;

        async void RequestThread() {
            while (listener.IsListening) {
                string url = string.Empty;

                try {
                    // Yeah, this blocks, but that's the whole point of this thread
                    // Note: the number of threads that are dispatching requets in no way limits the number of "open" requests that we can have
                    HttpListenerContext context = await listener.GetContextAsync();
                    
                    context.Response.KeepAlive = false;
                    context.Response.ContentEncoding = Encoding.UTF8;

                    // For this demo we only support GET
                    if (context.Request.HttpMethod != "GET") {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                    }

                    var request = context.Request;

                    if (request.Url.AbsolutePath.StartsWith("/~/")) {
                        context.Response.Abort();
                        continue;
                    }

                    if (request.Url.AbsolutePath == "/favicon.ico") continue;
                    Logging.Message($"REQ {request.Url.AbsolutePath} | {request.HttpMethod} | {request.UserHostName} \n{request.Headers.ToString()} ");
                    string url_path = Uri.UnescapeDataString(request.Url.AbsolutePath);

                    string passdir = CurrentConfig.server.values["server"]["passdir"].get_string().Trim();
                    if (!url_path.StartsWith($"/{passdir}/") || url_path == ($"/{passdir}/" )) {
                        context.Response.Abort();
                        continue;
                    } else {
                        url_path = url_path.Remove(0, 5);
                    }

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
                        Logging.Message($"Accessing share: {share_name}");
                    } else {
                        Logging.Error($"Client requested share which doesn't exist: {share_name} {url_path}");
                        context.Response.Close();
                        continue;
                    }
                    
                    url_path = url_path.Remove(0, share_name.Length);
                    string absolute_on_disk_path = folder_path.Replace("\\", "/") + Uri.UnescapeDataString(url_path);
                    byte[] data;

                    context.Response.AddHeader("X-Frame-Options", "DENY");
                    context.Response.AddHeader("Content-Disposition", "inline");
                    context.Response.AddHeader("Accept-ranges", "none");
                    context.Response.SendChunked = false;
                    //context.Response.AddHeader("Cache-Control", "no-cache");
                    //response.AddHeader("Link", "<base_css.css>;rel=stylesheet;media=all");

                    //Requested CSS file
                    if (request.Url.AbsolutePath.EndsWith("base_css.css")) {
                        absolute_on_disk_path = "base_css.css";
                        Logging.Message("Requesting CSS");

                        data = Encoding.UTF8.GetBytes(CSS);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = data.LongLength;

                        context.Response.OutputStream.BeginWrite(data, 0, data.Length, result => {
                            context.Response.OutputStream.EndWrite(result);
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.StatusDescription = "400 OK";
                            context.Response.Close();
                            Logging.Message("Sent CSS");
                        }, context.Response);

                    //Requested a directory
                    } else if (Directory.Exists(absolute_on_disk_path)) {
                        if (!show_dirs && url_path != "/" ) {
                            Logging.Error($"Attempted to browse outside of share \"{share_name}\" with directories off");
                            page_content = "";
                        } else {
                            page_content = FileListing.BuildListing(folder_path, request.UserHostName, url_path, share_name);
                        }

                        data = Encoding.UTF8.GetBytes(page_data);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = data.LongLength;

                        context.Response.OutputStream.BeginWrite(data, 0, data.Length, result => {
                            context.Response.OutputStream.EndWrite(result);
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.StatusDescription = "400 OK";
                            context.Response.Close();
                            Logging.Message($"Sent directory listing for {url_path}");
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

                        if (!show_dirs && url_path.Count(x => x == '/') > 1 ) {
                            Logging.Error($"Attempted to open file outside of share \"{share_name}\" with directories off");
                            continue;
                        }

                        Logging.Message($"Filename: {absolute_on_disk_path} | Content-type: {mimetype}");

                        context.Response.AddHeader("filename", request.Url.AbsolutePath.Remove(0, 1));
                        context.Response.ContentType = mimetype;                        
                        context.Response.SendChunked = false;

                        Logging.Message("Starting write");

                        FileStream fs = File.OpenRead(absolute_on_disk_path);
                        context.Response.ContentLength64 = fs.Length;

                        Task t = fs.CopyToAsync(context.Response.OutputStream, 1024, CancellationToken.None);
                        t.GetAwaiter().OnCompleted(() => {
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.StatusDescription = "400 OK";
                            context.Response.OutputStream.Close();
                            context.Response.Close();
                            Logging.Message($"Finished write on {url_path}");
                            fs.Close();
                        });
                                                
                    //User gave a very fail URL
                    } else {
                        page_content = $"<b>NOT FOUND</b>";
                        data = Encoding.UTF8.GetBytes(page_data);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = data.LongLength;

                        context.Response.OutputStream.BeginWrite(data, 0, data.Length, result => {
                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            context.Response.StatusDescription = "404 NOT FOUND";
                            context.Response.OutputStream.Close();
                            context.Response.Close();
                            Logging.Message("Finished writing 404");
                        }, context.Response);
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


        string CSS = "";

        public void StartServer() {
            page_data = File.ReadAllText("base_page");
            CSS = File.ReadAllText("base_css.css");
            listener = new HttpListener();
            //listener.IgnoreWriteExceptions = true;

            var port = CurrentConfig.server.values["server"]["port"].get_int();

            var prefixes = CurrentConfig.server.values["server"]["prefix"].ToString().Trim().Split(' ');
            dispatch_thread_count = CurrentConfig.server.values["server"]["threads"].get_int();
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