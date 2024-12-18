using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using HeyRed.Mime;
using ImageMagick;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole {
    public static class Conversion {

        internal struct cache_data {
            internal double birth;
            internal double life = 0;
            internal double age => DateTimeOffset.UtcNow.ToUnixTimeSeconds() - birth;

            internal void refresh() => birth = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            internal byte[] data;
            internal int length => data.Length;

            internal string mime = "";

            internal cache_data(double life, string mime) {
                this.life = life;
                this.mime = mime;
                birth = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        internal class ConversionCache {
            internal volatile Dictionary<string, cache_data> cache = new Dictionary<string, cache_data>();

            static readonly double max_age = 86400 / 4;

            public bool currently_pruning = false;

            internal void StartPruning(CancellationToken cancellation_token) {
                Task.Run(() => {
                    currently_pruning = true;
                    Logging.ThreadMessage($"Started pruning thread", "Cache", 5);
                    while (true) {
                        Prune();
                    }
                }, cancellation_token).ContinueWith(a => {
                    currently_pruning = false;
                });
            }

            internal bool Test(string key) => cache.ContainsKey(key);
            internal void Refresh(string name) => cache[name].refresh();

            private void Prune() {
            restart:
                foreach (var key in cache.Keys) {
                    if (cache[key].age > cache[key].life) {
                        cache.Remove(key);
                        Logging.ThreadMessage($"Pruned {key} from cache", "Cache", 5);
                        goto restart;
                    }
                }

                Thread.Sleep(1000);
            }

            internal void Store(string name, cache_data cmp4) {
                if (Test(name)) return;
                cache.Add(name, cmp4);
                Logging.ThreadMessage($"Stored {name} in cache", "Cache", 5);
            }
        }

        public static bool convert_images_list() => CurrentConfig.server["list"]["convert_images_automatically"].ToBool();
        public static bool convert_videos_list() => CurrentConfig.server["list"]["convert_videos_automatically"].ToBool();
        public static bool convert_audio_list() => CurrentConfig.server["list"]["convert_audio_automatically"].ToBool();

        public static bool convert_images_gallery() => CurrentConfig.server["gallery"]["convert_images_automatically"].ToBool();
        public static bool convert_videos_gallery() => CurrentConfig.server["gallery"]["convert_videos_automatically"].ToBool();
        public static bool convert_audio_gallery() => CurrentConfig.server["gallery"]["convert_audio_automatically"].ToBool();

        public static string CheckConversionList(string mime) {
            return CheckConversion(mime, convert_images_list(), convert_videos_list(), convert_audio_list());
        }

        public static string CheckConversionGallery(string mime) {
            return CheckConversion(mime, convert_images_gallery(), convert_videos_gallery(), convert_audio_gallery());
        }
        const string date_fmt = "ddd, dd MMM yyyy HH:mm:ss 'GMT'";

        public static long ParseDateHeaderToSeconds(string header) {
            if (header == null) return 0;

            DateTime dt;

            DateTime.TryParseExact(
               header,
               date_fmt,
               CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
               out dt
           );

            return dt.ToFileTimeUtc();
        }

        public static bool IsValidImage(string mime) => 
            mime.StartsWith("image") || mime == "application/postscript" || mime == "application/pdf";


        const string transcode_url = "/transcode";
        const string png_url = "/to_png";
        const string jpg_url = "/to_jpg";
        public static string CheckConversion(string mime, bool images, bool videos, bool audio) {
            if (mime == "application/postscript") return png_url;

            if (mime.StartsWith("image")) {
                if (!images) return "";

                //raw formats
                if (mime.EndsWith("/dng")) return jpg_url;
                if (mime.EndsWith("/raw")) return jpg_url;

                //should work without this, doesn't in chome
                //if (mime.EndsWith("/avif")) return "/to_png";

                //adobe
                if (mime.EndsWith("/vnd.adobe.photoshop")) return png_url;

            } else if (mime.StartsWith("video")) {
                if (!videos) return "";

                //wmv
                if (mime.EndsWith("x-ms-wmv")) return transcode_url;
                //avi
                if (mime.EndsWith("x-msvideo")) return transcode_url;
                //mkv
                if (mime.EndsWith("x-matroska")) return transcode_url;
                //quicktime
                if (mime.EndsWith("quicktime")) return transcode_url;
                //mpeg
                if (mime.EndsWith("mpeg")) return transcode_url;
                //3gpp
                if (mime.EndsWith("3gpp")) return transcode_url;
            } else if (mime.StartsWith("audio")) {
                if (!audio) return "";

                //wma
                if (mime.EndsWith("x-ms-wma")) return transcode_url;
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


        public static class Audio {

        }

        public static class Video {
            /*
            string InsertStream() {
                page_content = """
                        <video id="stream_video" controls preload="auto" autoplay>
                        """;
                page_content += $"<source src=\"http://{request.UserHostName}/{passdir}/transcode/{share_name}{url_path}\" type =\"video/mp4\">";
                page_content += """                        
                        </video>
                        <script>
                            const vid = document.getElementById("stream_video");
                            vid.onplay = function() {
                                console.log("Video is playing");
                                // For example, change the video source or other attributes:
                                videoElement.volume = 0.5; // Set the volume dynamically
                            };
                            vid.play().then(() => console.log("fart"));
                        </script>
                        """;// TURN THIS ALL INTO A METHOD TO INSERT VIDEO STREAMS IN CONVERSION BRB
            }
            */
            public async static void transcode_mp4_full(FileInfo file, HttpListenerContext context) {
                long tid = DateTime.Now.Ticks;

                var anal = FFProbe.Analyse(file.FullName);

                var has_range = !string.IsNullOrEmpty(context.Request.Headers.Get("Range"));
                var range = context.Request.Headers.Get("Range");

                try {
                    Logging.ThreadMessage($"{file.Name} :: Sending transcoded MP4 data", "CONVERT:MP4", tid);

                    context.Response.ContentType = "video/mp4";

                    context.Response.AddHeader("Accept-Ranges", "none");
                    context.Response.SendChunked = false;

                    context.Response.AddHeader("X-Content-Duration", anal.Duration.TotalSeconds.ToString("F2"));
                    context.Response.AddHeader("Content-Duration", anal.Duration.TotalSeconds.ToString("F2"));
                    //context.Response.ContentLength64 = (long)((anal.Duration.TotalSeconds * 1000000) / 8.0);
                    //var bytes = context.Response.ContentLength64 / 1024;

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.StatusDescription = "200 OK";

                    FFMpegArguments
                        .FromFileInput(file.FullName)
                        .OutputToPipe(new StreamPipeSink(context.Response.OutputStream), options => options
                            .ForceFormat("mp4")
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("aac")

                            .UsingMultithreading(true)
                            .UsingThreads(CurrentConfig.server["transcode"]["threads_per_video_conversion"].ToInt())

                            .WithCustomArgument("-map_metadata 0")

                            .ForcePixelFormat("yuv420p")
                            //.WithConstantRateFactor(30)
                            .WithVideoBitrate(CurrentConfig.server["transcode"]["bit_rate_kb"].ToInt() * 1024)
                            .WithSpeedPreset(Speed.VeryFast)
                            .WithFastStart()

                            .WithCustomArgument("-loglevel verbose")
                            .WithCustomArgument("-movflags frag_keyframe+empty_moov")
                            .WithCustomArgument("-movflags +faststart")
                            .WithCustomArgument($"-ab 240k")

                        ).ProcessAsynchronously().ContinueWith(t => {
                             Logging.ThreadMessage($"{file.Name} :: Finished sending data", "CONVERT:MP4", tid);                            
                        }, CurrentConfig.cancellation_token);

                } catch (Exception ex) {
                    Logging.ThreadError($"{file.Name} :: {ex.Message}", "CONVERT:MP4", tid);
                }
            }
        }
    }
}
