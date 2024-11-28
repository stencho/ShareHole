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
        public string share_name = "";

        Dictionary<string, string> mime_dict = MIME.get_MIME_dict();
        HttpListener listener;
        
        string page_title = "";
        string page_content = "";

        public void StopServer() {
            run = false;
        }

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
        public async Task ServePage() {
            while (run) {
                HttpListenerContext context = await listener.GetContextAsync();

                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                Logging.Message($"REQ {request.Url.AbsolutePath} | {request.HttpMethod} | {request.UserHostName} ");

                string url_path = request.Url.AbsolutePath;                
                string folder_path = CurrentConfig.shares[share_name]["path"].ToString();
                
                while (url_path.StartsWith('/')) {
                    url_path = url_path.Remove(0,1);
                }

                Logging.Message(folder_path.Replace("\\","/") + Uri.UnescapeDataString(url_path));
                string absolute_on_disk_path = folder_path.Replace("\\", "/") + Uri.UnescapeDataString(url_path);
                byte[] data;

                if (Directory.Exists(absolute_on_disk_path)) {
                    page_content = folder_path + "\n";
                    page_content += FileListing.BuildListing(folder_path, request.UserHostName, url_path);

                    data = Encoding.UTF8.GetBytes(page_data);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = data.LongLength;

                    try {
                        await response.OutputStream.WriteAsync(data, 0, data.Length);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.StatusDescription = "OK";
                        response.Close();
                    } catch (HttpListenerException ex) {
                        response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                        response.StatusDescription = "429 Too Many Requests";
                        response.Close();
                    }

                } else if (File.Exists(absolute_on_disk_path)) {
                    try {
                        //var mt = MimeTypes                    
                        FileStream fs = File.OpenRead(absolute_on_disk_path);

                        string mimetype;
                        try {
                            mimetype = MimeTypesMap.GetMimeType(absolute_on_disk_path);
                        } catch {
                            mimetype = "application/octet-stream";
                        }
                        Logging.Warning($"Content-type: {mimetype}");
                        response.ContentType = mimetype;


                        response.AddHeader("Content-Disposition", "inline");
                        response.AddHeader("Cache-Control", "no-cache");
                        response.AddHeader("Link", "<base_css.css>;rel=stylesheet;media=all");
                        response.AddHeader("filename", request.Url.AbsolutePath.Remove(0, 1));
                        response.ContentLength64 = fs.Length;
                        response.SendChunked = false;
                        /*
                        byte[] buffer = new byte[64 * 1024];
                        int read;
                        using (BinaryWriter bw = new BinaryWriter(response.OutputStream)) {
                            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0) {
                                bw.Write(buffer, 0, read);
                                bw.Flush(); //seems to have no effect
                            }

                            bw.Close();
                        }*/

                        data = Encoding.Unicode.GetBytes(page_data);
                        Logging.Message("Started serving file???");
                        Task serve_file = ServeFile(context, response, fs, data);
                        //serve_file.Start();

                    } catch (HttpListenerException ex) {
                        response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                        response.StatusDescription = "429 Too Many Requests";
                        response.Close();
                    }


                } else {
                    page_content = "<b>NOT FOUND</b>";
                    data = Encoding.UTF8.GetBytes(page_data);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = data.LongLength;
                    try {
                        await response.OutputStream.WriteAsync(data, 0, data.Length);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.StatusDescription = "OK";
                        response.Close();
                    } catch (HttpListenerException ex) {
                        response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                        response.StatusDescription = "429 Too Many Requests";
                        response.Close();
                    }
                }
            }

        }

        public async Task ServeFile(HttpListenerContext context, HttpListenerResponse response, FileStream fs, byte[] data) {
            byte[] buffer = new byte[1024];
            int read;
            response.KeepAlive = false;
            response.StatusCode = (int)HttpStatusCode.OK;
            response.StatusDescription = "OK";

            using (BinaryWriter bw = new BinaryWriter(response.OutputStream)) {
                while (listener.IsListening && response.OutputStream.CanWrite && (read = fs.Read(buffer, 0, buffer.Length)) > 0) {
                    bw.Write(buffer, 0, read);
                    //bw.Flush(); //seems to have no effect
                }

                bw.Close();
            }
            Logging.Message("Finished serving file???");
            response.Close();
            fs.Close();
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
                    Logging.Message($"REQ {request.Url.AbsolutePath} | {request.HttpMethod} | {request.UserHostName} \n{request.Headers.ToString()} ");
                    string url_path = Uri.UnescapeDataString(request.Url.AbsolutePath);
                    string folder_path = CurrentConfig.shares[share_name]["path"].ToString();

                    if (Environment.OSVersion.Platform != PlatformID.Unix) {
                        while (url_path.StartsWith('/')) {
                            url_path = url_path.Remove(0, 1);
                        }
                    }
                    string absolute_on_disk_path = folder_path.Replace("\\", "/") + Uri.UnescapeDataString(url_path);
                    byte[] data;

                    string mimetype;
                    try {
                        mimetype = MimeTypesMap.GetMimeType(absolute_on_disk_path);
                    } catch {
                        mimetype = "application/octet-stream";
                    }

                    Logging.Warning($"Content-type: {mimetype}");
                    response.ContentType = mimetype;
                    response.AddHeader("X-Frame-Options", "SAMEORIGIN");


                    if (Directory.Exists(absolute_on_disk_path)) {
                        try {
                            page_content = absolute_on_disk_path + "\n";
                            page_content += FileListing.BuildListing(folder_path, request.UserHostName, url_path);

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
                            /*
                            response.OutputStream.BeginWrite(data, 0, data.Length, result => {
                                response.OutputStream.EndWrite(result);
                                response.Close();
                            }, response);*/
                        } catch (HttpListenerException ex) {
                            Logging.Error(ex.Message);
                            response.Close();
                        }

                    } else if (File.Exists(absolute_on_disk_path)) {
                        try {
                            Logging.Message($"file: {absolute_on_disk_path}");
                            FileStream fs = File.OpenRead(absolute_on_disk_path);

                            response.AddHeader("Content-Disposition", "inline");
                            response.AddHeader("Cache-Control", "no-cache");
                            response.AddHeader("X-Frame-Options", "allowall");
                            response.AddHeader("Link", "<base_css.css>;rel=stylesheet;media=all");

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
                            /*
                            response.OutputStream.BeginWrite(data, 0, data.Length, result => {
                                response.OutputStream.EndWrite(result);
                                response.Close();
                            }, response);
                            */
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
        }


        public void StartServer(string share_name) {
            page_data = File.ReadAllText("base_page");

            listener = new HttpListener();

            this.share_name = share_name;

            var port = CurrentConfig.shares[share_name]["port"].get_int();

            var prefixes = CurrentConfig.shares[share_name]["prefix"].ToString().Trim().Split(' ');

            for (int i = 0; i < prefixes.Length; i++) {
                string prefix = prefixes[i].Trim();
                if (prefix.StartsWith("http://")) prefix = prefix.Remove(0, 7);
                if (prefix.StartsWith("https://")) prefix = prefix.Remove(0, 8);
                while (prefix.EndsWith('/')) prefix.Remove(prefix.Length - 1, 1);

                //listener.Prefixes.Add($"http://*{port}/");
                listener.Prefixes.Add($"http://{prefix}:{port}/");
                Logging.Message("Using prefix: " + $"http://{prefix}:{port}/");
            }

            listener.Start();

            dispatch_threads = new Thread[dispatch_thread_count];
            Logging.Message($"Starting server for share '{share_name}' on port {port}");

            for (int i = 0; i < dispatch_thread_count; i++) {
                dispatch_threads[i] = new Thread(RequestThread);
                dispatch_threads[i].Start();
                Logging.Message($"[{share_name}] started thread {i}");
            }


            //Task listener_task = ServePage();
            //listener_task.GetAwaiter().GetResult();
            //listener.Close();
            
        }

    }
}