using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using HeyRed.Mime;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole {
    public static class Conversion {

        public static string CheckConversion(string mime) {
            if (mime.StartsWith("image")) {
                //raw formats
                if (mime.EndsWith("/dng")) return "/to_jpg";
                if (mime.EndsWith("/raw")) return "/to_jpg";

                //should work without this, doesn't in chome
                if (mime.EndsWith("/avif")) return "/to_png";

                //adobe
                if (mime.EndsWith("/vnd.adobe.photoshop")) return "/to_png";

            } else if (mime.StartsWith("video")) {
                if (mime.EndsWith("x-ms-wmv")) return "/to_mp4";
                //if (mime.EndsWith("x-msvideo")) return "/to_mp4";
                //if (mime.EndsWith("x-matroska")) return "/to_mp4";
            }

            return "";
        }

        public static string GetMimeTypeOrOctet(string fn) {
            string mimetype;

            var fi = new FileInfo(fn);
            if (fi.Extension.ToLower() == ".dng") return "image/dng";
            if (fi.Extension.ToLower() == ".raw") return "image/raw";
            if (fi.Extension.ToLower() == ".avif") return "image/avif";
            if (fi.Extension.ToLower() == ".avi") return "video/x-msvideo";

            try {
                mimetype = MimeTypesMap.GetMimeType(fn);
            } catch {
                mimetype = "application/octet-stream";
            }
            return mimetype;
        }

        public static string GetMimeTypeOrOctetMinusExt(string fn) {
            string mimetype;

            var fi = new FileInfo(fn);
            if (fi.Extension.ToLower() == ".dng") return "image";
            if (fi.Extension.ToLower() == ".raw") return "image";
            if (fi.Extension.ToLower() == ".avif") return "image";
            if (fi.Extension.ToLower() == ".avi") return "video/x-msvideo";

            try {
                mimetype = MimeTypesMap.GetMimeType(fn.ToLower());
            } catch {
                mimetype = "application/octet-stream";
            }
            return mimetype.Substring(0, mimetype.IndexOf("/"));
        }

        public static class Image {
            public static void ConvertToJpeg(MagickImage image, uint compression_quality = 85) {
                image.Settings.Format = MagickFormat.Jpg;
                image.Settings.Compression = CompressionMethod.JPEG;
                image.Quality = compression_quality;
            }

            public static void ConvertToPng(MagickImage image) {
                image.Settings.Format = MagickFormat.Png;
                image.Quality = 100;
            }
            public static bool IsRAW(string mime) {
                if (mime == "image/dng") return true;
                else if (mime == "image/raw") return true;
                else return false;
            }
        }


        public static class Video {
            internal struct video_data {
                internal double birth;
                internal double life = 0;
                internal double age => DateTimeOffset.UtcNow.ToUnixTimeSeconds() - birth;        
                
                internal void refresh() => birth = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                internal byte[] data;
                internal int length => data.Length;

                internal string mime = "";

                internal video_data(double life, string mime) {
                    this.life = life;
                    this.mime = mime;
                    birth = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
            }

            internal static class VideoCache {
                internal static volatile Dictionary<string, video_data> cache = new Dictionary<string, video_data>();

                static readonly double max_age = 86400 / 4;

                public static bool currently_pruning = false;

                internal static void StartPruning() {
                    Task.Run(() => {
                        currently_pruning = true;
                        Logging.ThreadMessage($"Started pruning thread", "MP4 Cache", 5);
                        while (true) {
                            Prune();
                        }
                    }, CurrentConfig.cancellation_token).ContinueWith(a => {
                        currently_pruning = false;
                    });

                }

                internal static bool Test(string key) => cache.ContainsKey(key);
                internal static void Refresh(string name) => cache[name].refresh();

                private static void Prune() {
                    restart:
                    foreach (var key in cache.Keys) {
                        if (cache[key].age > cache[key].life) { 
                            cache.Remove(key);
                            Logging.ThreadMessage($"Pruned {key} from cache", "MP4 Cache", 5);
                            goto restart;
                        }
                    }

                    Thread.Sleep(1000);
                }

                internal static void Store(string name, video_data cmp4) {
                    if (Test(name)) return;
                    cache.Add(name, cmp4);
                    Logging.ThreadMessage($"Stored {name} in cache", "MP4 Cache", 5);
                }
            }

            public async static void MP4ByteStream(FileInfo file, HttpListenerContext context) {                
                try {
                    Logging.Message($"Converting {file.Name} to MP4");
                    var anal = FFProbe.Analyse(file.FullName);
                    video_data data;

                    if (!VideoCache.Test(file.FullName)) {
                        using (var ms = new MemoryStream()) {
                            await FFMpegArguments
                                .FromFileInput(file//, options => options
                                                   //.WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                                    )
                                .OutputToPipe(new StreamPipeSink(ms), options => options
                                    .ForceFormat("mp4")
                                    .WithVideoCodec("libx264")
                                    .WithAudioCodec("aac")
                                    .UsingMultithreading(true)

                                    .UsingThreads(CurrentConfig.server["conversion"]["threads_per_video_conversion"].get_int())
                                    .WithVideoBitrate(CurrentConfig.server["conversion"]["mp4_bitrate"].get_int())

                                    //.WithFastStart()

                                    .WithCustomArgument("-loglevel verbose")
                                    .WithCustomArgument("-movflags frag_keyframe+empty_moov")

                                ).ProcessAsynchronously();

                            data = new video_data(anal.Duration.TotalSeconds + (60 * 10), "video/mp4");

                            data.data = new byte[ms.Length];
                            ms.Seek(0, SeekOrigin.Begin);

                            using (var data_stream = new MemoryStream(data.data)) {
                                await ms.CopyToAsync(data_stream);
                            }

                            VideoCache.Store(file.FullName, data);

                        }
                    } else {
                        data = VideoCache.cache[file.FullName];
                    }

                    //implement header checks for range
                    var r = context.Request.Headers["Range"];

                    context.Response.ContentType = "video/mp4";
                    context.Response.ContentLength64 = data.length;
                    //context.Response.AddHeader("Accept-ranges", "none");
                    context.Response.SendChunked = false;

                    using (var ds = new MemoryStream(data.data)) {
                        ds.CopyToAsync(context.Response.OutputStream, CurrentConfig.cancellation_token).ContinueWith(res => {

                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.StatusDescription = "400 OK";
                            context.Response.Close();

                            Logging.Message($"Done copying MP4 chunk of {file.Name}");
                        });
                    }
                } catch (Exception ex) {
                    Logging.Error($"MP4 {file.Name} :: {ex.Message}");
                }
            }

            public async static void WebmByteStream(FileInfo file, HttpListenerContext context) {
                Logging.Message($"Attempting to convert {file.Name} to MP4");

                try {
                    var anal = FFProbe.Analyse(file.FullName);
                    video_data data;

                    if (!VideoCache.Test(file.FullName)) {
                        var ms = new MemoryStream();
                        await FFMpegArguments
                            .FromFileInput(file)
                            .OutputToPipe(new StreamPipeSink(ms), options =>
                                options
                                .ForceFormat("webm")
                                .WithVideoCodec("libvpx-vp9")
                                .WithAudioCodec("libopus")
                                .UsingMultithreading(true)
                                .UsingThreads(CurrentConfig.server["conversion"]["threads_per_video_conversion"].get_int())
                                .WithFastStart()

                                .WithVideoBitrate((int)anal.PrimaryVideoStream.BitRate)
                                .WithCustomArgument("-loglevel verbose")                                

                            ).ProcessAsynchronously();

                        data = new video_data(anal.Duration.TotalSeconds + (60 * 10), "video/webm");

                        data.data = new byte[ms.Length];
                        ms.Seek(0, SeekOrigin.Begin);

                        using (var data_stream = new MemoryStream(data.data)) {
                            await ms.CopyToAsync(data_stream);
                        }

                        VideoCache.Store(file.FullName, data);

                        ms.Close();
                    } else {
                        data = VideoCache.cache[file.FullName];
                    }

                    context.Response.ContentType = "video/webm";
                    context.Response.ContentLength64 = data.length;

                    var ds = new MemoryStream(data.data);
                    await ds.CopyToAsync(context.Response.OutputStream).ContinueWith(a => {
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.StatusDescription = "400 OK";
                        context.Response.Close();
                        Logging.Message($"Done copying MP4 chunk of {file.Name}");
                        ds.Close();
                    }, CurrentConfig.cancellation_token);

                } catch (Exception ex) {
                    Logging.Error($"Webm {file.Name} :: {ex.Message}");
                }
            }
        }
    }
}
