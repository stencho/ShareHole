using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole {
    internal class PartialFileSend {
        public static void StartNewSend(string filename, string mime, HttpListenerContext context) {
            Task.Run(() => { send_file_partial(filename, mime, context); }, CurrentConfig.cancellation_token);
        }
        static (long start, long end) ParseRequestRangeHeader(string range_value, long file_size) {
            (long start, long end) output = (-1,-1);

            if (!range_value.StartsWith("bytes=")) {
                Logging.Error($"Invalid range header: {range_value}");
                return (0, file_size);
            }

            string rv = range_value.Remove(0, "bytes=".Length);
            int length;

            if (rv.Contains("/")) rv.Remove(rv.IndexOf("/"));

            if (rv.Contains("-")) {
                string[] split = rv.Split("-");

                if (split.Length == 2) {
                    output.start = int.Parse(split[0]);

                    if (long.TryParse(split[1], out output.end)) {
                        return (output.start, output.end);
                    } else {
                        return (output.start, file_size);
                    }

                } else {
                    return (0, file_size);
                }


            } else {
                Logging.Error($"Invalid range header: {range_value}");
                return (0, file_size);
            }

        }

        static async void send_file_partial(string filename, string mime, HttpListenerContext context) {
            FileInfo fi = new FileInfo(filename);

            var has_range = !string.IsNullOrEmpty(context.Request.Headers.Get("Range"));
            var range = context.Request.Headers.Get("Range");

            var file_size = fi.Length;

            context.Response.AddHeader("Accept-Ranges", "bytes");
            context.Response.AddHeader("Content-Type", mime);

            context.Response.ContentLength64 = file_size;

            if (has_range) {
                var range_info = ParseRequestRangeHeader(range, file_size);

                context.Response.AddHeader("Content-Ranges", $"bytes={range_info.start}-{range_info.end}/{file_size}");

                FileStream fs = File.OpenRead(filename);

                //fs.BeginWrite(context.Response.OutputStream, 0, );

                //fs.Seek(range_info.start, SeekOrigin.Begin);
                fs.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                    try {
                        Logging.Message($"Wrote range {range_info.start}-{range_info.end}/{file_size}");

                        context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
                        context.Response.StatusDescription = "206 PARTIAL CONTENT";
                        context.Response.Close();

                        //Logging.Message($"Finished writing chunk to {fi.Name}");
                        fs.Close();

                    } catch (HttpListenerException ex) {
                        Logging.Error($"{ex.Message}");
                        fs.Close();
                    }
                }, CurrentConfig.cancellation_token);

                
            } else {
                Logging.Message($"Got file request, start streaming {fi.Name} of length {file_size}");

                FileStream fs = File.OpenRead(filename);

                fs.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {

                    try {
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.StatusDescription = "400 OK";
                        context.Response.OutputStream.Close();
                        context.Response.Close();

                        Logging.Message($"Finished writing {fi.Name}");
                        fs.Close();

                    } catch (HttpListenerException ex) {
                        Logging.Error($"{ex.Message}");
                        fs.Close();
                    }
                }, CurrentConfig.cancellation_token);
            }
        }
    }
}
