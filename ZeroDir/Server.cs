using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mime;
using HeyRed.Mime;

namespace ZeroDir {
    public static class  Server {
        public static Configuration config;
    }

    public class HttpServer {
        static Dictionary<string, string> mime_dict = MIME.get_MIME_dict();
        static HttpListener listener;

        static string url = "http://localhost:8080/";

        static string base_path = "";

        static string page_title = "";
        static string page_content = "";

        static string page_data => new string (
            "<!DOCTYPE>" +
            "<html>" +
            "  <head>" +
            $"    <title>{page_title}</title>" +
            "  </head>" +
            "  <body>" +
            $"{page_content}" +
            "  </body>" +
            "</html>");

        public static async Task HandleConnections() {
            bool run = true;

            while(run) {
                HttpListenerContext context = await listener.GetContextAsync();

                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                var combo_path = Uri.UnescapeDataString(Path.Join(base_path, request.Url.AbsolutePath.Remove(0,1)));
                var ext = new FileInfo(combo_path).Extension;
                if (ext.StartsWith('.')) ext = ext.Remove(0, 1);
                Console.WriteLine($"REQ {request.Url.AbsolutePath} [{combo_path}] | {request.HttpMethod} | {request.UserHostName} ");

                Logging.Info(combo_path);
                //if (request.HttpMethod == "POST") {}//do stuff

                byte[] data;
                data = Encoding.UTF8.GetBytes(page_data);
                if (Directory.Exists(combo_path)) {
                    page_content = FileListing.BuildListing(combo_path);
                    data = Encoding.UTF8.GetBytes(page_data);
                    response.ContentType = "text/html";
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
                else if (File.Exists(combo_path)) {
                    try {
                        //var mt = MimeTypes                    
                        using (FileStream fs = File.OpenRead(combo_path)) {

                            string mimetype;
                            try {
                                mimetype = MimeTypesMap.GetMimeType(combo_path);
                            } catch {
                                mimetype = "application/octet-stream";
                            }
                            Logging.Warning($"Content-type: {mimetype}");
                            response.ContentType = mimetype;


                            response.AddHeader("Content-Disposition", "inline");
                            response.AddHeader("Cache-Control", "no-cache");
                            response.AddHeader("filename", request.Url.AbsolutePath.Remove(0, 1));
                            response.ContentLength64 = fs.Length;
                            response.SendChunked = false;
                            

                        
                            byte[] buffer = new byte[64 * 1024];
                            int read;
                            using (BinaryWriter bw = new BinaryWriter(response.OutputStream)) {
                                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0) {
                                    bw.Write(buffer, 0, read);
                                    bw.Flush(); //seems to have no effect
                                }

                                bw.Close();
                            }
                        
                        }   
                        await response.OutputStream.WriteAsync(data, 0, data.Length);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.StatusDescription = "OK";
                        response.Close();
                    } catch (HttpListenerException ex) {
                        response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                        response.StatusDescription = "429 Too Many Requests";
                        response.Close();
                    }


                } else {
                    page_content = "<b>NOT FOUND</b>";
                    data = Encoding.UTF8.GetBytes(page_data);
                    response.ContentType = "text/html";
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
                ///response.ContentEncoding = Encoding.UTF8;
                //response.ContentLength64 = data.LongLength;

            }
        }



        static bool running = true;
        public static void start(string root_folder) {
            while (running) {
                base_path = root_folder;

                listener = new HttpListener();
                listener.Prefixes.Add(url);
                listener.Start();
                Logging.Info($"Listening for connections on {url}, serving folder {root_folder}");

                Task listener_task = HandleConnections();
                listener_task.GetAwaiter().GetResult();
                listener.Close();
            }
        }

    }
}