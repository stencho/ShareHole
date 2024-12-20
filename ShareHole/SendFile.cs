using System.Net;
using System.Text;

namespace ShareHole {
    public class Send {
        public static void OK(HttpListenerContext context) {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.StatusDescription = "200 OK";
            Logging.Error("200");
            try {
                context.Response.Close();
            } catch (HttpListenerException e) { }
        }

        public static void Error404(string page_content, HttpListenerContext context) {
            var data = Encoding.UTF8.GetBytes(ShareServer.page_content_strings_replaced(page_content, ""));
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = data.LongLength;

            using (MemoryStream ms = new MemoryStream(data, false)) {
                var task = ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.StatusDescription = "404 NOT FOUND";
                    context.Response.Close();
                }, CurrentConfig.cancellation_token);
            }
            Logging.Error("404");

        }
        public static void ErrorBadRequest(string page_content, HttpListenerContext context) {
            var data = Encoding.UTF8.GetBytes(ShareServer.page_content_strings_replaced(page_content, ""));
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = data.LongLength;

            using (MemoryStream ms = new MemoryStream(data, false)) {
                var task = ms.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.StatusDescription = "400 BAD REQUEST";
                    context.Response.Close();
                }, CurrentConfig.cancellation_token);
                Logging.Error("400");
            }
        }

        public static void FileWithRanges(string filename, string mime, HttpListenerContext context) {
            Task.Run(() => { send_file_ranges(filename, mime, context); }, CurrentConfig.cancellation_token);
        }   

        public static (long start, long end, long length) ParseRequestRangeHeader(string range_value, long file_size) {
            (long start, long end) output = (-1,-1);

            if (!range_value.StartsWith("bytes=")) {
                Logging.Error($"Invalid range header: {range_value}");
                return (0, file_size-1, file_size);
            }

            string rv = range_value.Remove(0, "bytes=".Length);
            int length;

            if (rv.Contains("/")) rv.Remove(rv.IndexOf("/"));

            if (rv.Contains("-")) {
                string[] split = rv.Split("-");

                if (split.Length == 2) {
                    output.start = int.Parse(split[0]);

                    if (long.TryParse(split[1], out output.end)) {
                        return (output.start, output.end, output.end - output.start);
                    } else {
                        return (output.start, file_size - 1, file_size - output.start);
                    }

                } else {
                    return (0, file_size - 1, file_size);
                }

            } else {
                Logging.Error($"Invalid range header: {range_value}");
                return (0, file_size-1, file_size);
            }
        }


        static async void send_file(FileInfo file, string mime, HttpListenerContext context) {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.StatusDescription = "200 OK";

            FileStream fs = File.OpenRead(file.FullName);
            context.Response.ContentLength64 = fs.Length;

            fs.CopyToAsync(context.Response.OutputStream, CurrentConfig.cancellation_token).ContinueWith(a => {
                try {
                    //context.Response.OutputStream.Close();
                    if (CurrentConfig.LogLevel == Logging.LogLevel.ALL)
                        Logging.Warning($"Finished writing {file.Name}");
                    fs.Close();
                } catch (HttpListenerException ex) {
                    Logging.Error($"{ex.Message}");
                    fs.Close();
                }
            }, CurrentConfig.cancellation_token);
        }

        static async void send_file_ranges(string filename, string mime, HttpListenerContext context) {
            FileInfo fi = new FileInfo(filename);
            
            //check for range header
            var has_range = !string.IsNullOrEmpty(context.Request.Headers.Get("Range"));
            var range = context.Request.Headers.Get("Range");

            var file_size = fi.Length;

            //get range size from config
            var kb_size = CurrentConfig.server["server"]["transfer_buffer_size"].ToInt();
            if (kb_size <= 0) kb_size = 1;            
            long chunk_size = kb_size * 1024;
            if (chunk_size > int.MaxValue) chunk_size = int.MaxValue;

            context.Response.AddHeader("Accept-Ranges", "bytes");
            context.Response.AddHeader("Content-Type", mime);
            //context.Response.AddHeader("Transfer-Encoding", "chunked");
            context.Response.SendChunked = true;

            if (has_range) {
                var range_info = ParseRequestRangeHeader(range, file_size);

                context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
                context.Response.StatusDescription = "206 PARTIAL CONTENT";

                if (range_info.length > 0 && range_info.length < chunk_size) chunk_size = range_info.length;

                context.Response.ContentLength64 = chunk_size;

                if (CurrentConfig.LogLevel == Logging.LogLevel.ALL)
                    Logging.Message($"Wants range {range_info.start}-{range_info.start + chunk_size - 1} {(range_info.start + chunk_size - 1) - range_info.start}");

                context.Response.AddHeader("Content-Range", $"bytes {range_info.start}-{range_info.start + chunk_size - 1}/{file_size}");

                byte[] buffer = new byte[chunk_size];

                FileStream fs = File.OpenRead(filename);
                fs.Seek(range_info.start, SeekOrigin.Begin);
                await fs.ReadAsync(buffer, 0, buffer.Length, CurrentConfig.cancellation_token);
                fs.Close();

                using (MemoryStream buffer_stream = new MemoryStream(buffer)) {
                    buffer_stream.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                        try {
                            context.Response.OutputStream.Close();
                            if (CurrentConfig.LogLevel == Logging.LogLevel.ALL)
                                Logging.Message($"{fi.Name} :: Finished writing chunk \"{range_info.start}-{range_info.start + chunk_size - 1}/{file_size}\"");
                           

                        } catch (Exception ex) {
                            Logging.Error($"{ex.Message}");
                        }
                    }, CurrentConfig.cancellation_token);
                }

            } else {
                if (CurrentConfig.LogLevel == Logging.LogLevel.ALL)
                    Logging.Message($"Got file request, start streaming {fi.Name} of length {file_size}");

                send_file(fi, mime, context);
            }
        }
    }
}
