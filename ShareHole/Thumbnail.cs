using System.Net;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using ImageMagick;

namespace ShareHole {
    public class ThumbnailRequest
    {
        public FileInfo file;
        public byte[] thumbnail = null;

        public string mime_type = "";

        public bool thumbnail_ready = false;
        public bool thread_dispatched = false;

        public int thread_id = 0;

        public HttpListenerContext context;
        public HttpListenerResponse response => context.Response;
        public HttpListenerRequest request => context.Request;

        public ShareServer parent_server;

        public ThumbnailRequest(FileInfo file, HttpListenerContext context, ShareServer parent_server, string mime_type, int thread_id)
        {
            this.file = file;
            this.context = context;
            this.parent_server = parent_server;
            this.thread_id = thread_id;
            this.mime_type = mime_type;
        }
    }

    public static class ThumbnailManager
    {

        //cache for thumbnails which have been loaded at least once
        //static volatile Dictionary<string, (string mime, byte[] data)> thumbnail_cache = new Dictionary<string, (string mime, byte[] data)>();

        static ConcurrentCache<(string mime, byte[] data)> thumbnail_cache = new ConcurrentCache<(string mime, byte[] data)>();
        public static int ThumbsInCache => thumbnail_cache.Count;
        static int thumbnail_size => State.server["gallery"]["thumbnail_size"].ToInt();

       
        public static async Task CacheAllInShare(DirectoryInfo directory, string share_name, int max_workers, bool wait_for_workers) {
            int thumb_workers = 0;

            int c = 0;
            long bytes_processed = 0;
            long bytes_stored = 0;
            var start = DateTime.Now;


            Environment.SetEnvironmentVariable("MAGICK_THREAD_LIMIT", max_workers.ToString());

            foreach (var file in directory.GetFiles("*", new EnumerationOptions() { RecurseSubdirectories = true })) {
                if (State.cancellation_token.IsCancellationRequested) break;
                var mime = ConvertAndParse.GetMimeTypeOrOctet(file.FullName);

                if (ConvertAndParse.IsValidImage(mime) || mime.StartsWith("video")) {
                    while (thumb_workers >= max_workers) {
                        await Task.Delay(250);
                    }

                    Interlocked.Increment(ref thumb_workers);
                    State.StartTask(() => {
                        long b = 0;
                        BuildCache(file, mime, 64, -1, out b);
                        bytes_stored += b;
                        bytes_processed += file.Length;

                    }).ContinueWith(t => {
                        Interlocked.Decrement(ref thumb_workers);
                    });

                    c++;
                }
            }

            if (wait_for_workers) {
                while (thumb_workers > 0 && !State.cancellation_token.IsCancellationRequested)
                    Thread.Sleep(100);

                var end = DateTime.Now; 

                Logging.Message($"{c} items added to thumbnail cache in {(end - start).ToString(@"mm\:ss\.ff")}");
                Logging.Message($"{string.Format("{0:0.00}", bytes_processed / 1024.0 / 1024.0)} MB processed, " +
                    $"{string.Format("{0:0.00}", bytes_stored / 1024.0 / 1024.0)} MB stored");
                Logging.Warning($"Finished caching all thumbnails in [share] {share_name}");
            } 

            return;
        }

        static bool BuildCache(FileInfo fi, string mime_type, int thread_id, double life_time, out long file_length) {
            file_length = 0;
            int fail_count = 0;
            FileInfo file = new FileInfo(fi.FullName);

            if (ConvertAndParse.IsValidImage(mime_type)) {
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Building thumbnail for image {file.Name}", $"THUMB:{thread_id}", thread_id);

                using (MagickImage mi = new MagickImage(file.FullName)) {
                    if (mi.Orientation != OrientationType.Undefined)
                        mi.AutoOrient();

                    mi.Strip();

                    ConvertAndParse.Image.ConvertToPng(mi);

                    mi.Resize((uint)thumbnail_size, (uint)thumbnail_size);
                                        

                    try {
                        var ba = mi.ToByteArray();
                        thumbnail_cache.Store(file.FullName, ("image/png", ba), life_time);
                        file_length = ba.Length;
                        ba = null;
                        mi.Dispose();
                    } catch (Exception ex) {
                        Logging.Error($"{file.Name} :: {ex.Message}");
                    }
                    
                }
                
                //build one for a video
            } else if (mime_type.StartsWith("video")) {
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Building thumbnail for video {file.Name}", $"THUMB:{thread_id}", thread_id);
                fail_reattempt:
                try {
                    byte[] ffmpeg_thumb = null;

                    var anal = FFProbe.Analyse(file.FullName);

                    double final_x, final_y;

                    double aspect = anal.PrimaryVideoStream.Width / (double)anal.PrimaryVideoStream.Height;

                    if (aspect >= 0) {
                        final_x = thumbnail_size * aspect;
                        final_y = thumbnail_size;
                    } else {
                        final_y = thumbnail_size * aspect;
                        final_x = thumbnail_size;
                    }

                    using (var stream_output = new MemoryStream()) {
                        FFMpegArguments
                            .FromFileInput(file)
                            .OutputToPipe(new StreamPipeSink(stream_output), options =>
                                options.WithFrameOutputCount(1)
                                .WithVideoCodec(VideoCodec.Png)
                                .Resize((int)Math.Round(final_x), (int)Math.Round(final_y))
                                .ForceFormat("image2pipe")
                                .WithCustomArgument("-loglevel verbose")
                                ).ProcessSynchronously();

                        ffmpeg_thumb = stream_output.ToArray();

                        using (MemoryStream ms = new MemoryStream(ffmpeg_thumb)) {
                            using (MagickImage mi = new MagickImage(ms)) {
                                ConvertAndParse.Image.ConvertToPng(mi);

                                mi.Resize((uint)thumbnail_size, (uint)thumbnail_size);
                                var ba = mi.ToByteArray();
                                thumbnail_cache.Store(file.FullName, ("image/png", ba), life_time);
                                file_length = ba.Length;
                                ba = null;
                            }
                        }
                    }
                } catch (Exception ex) {
                    fail_count++;
                    Logging.Error($"{file.Name} :: {ex.Message}");
                    if (fail_count<5) goto fail_reattempt;
                    Logging.Error($"{file.Name} :: Failed too many times");
                }

            } else return false;
            return true;
        }
        

        public static async void BuildThumbnail(FileInfo file, HttpListenerContext context, ShareServer parent_server, string mime_type, int thread_id) {
            cache_fail:
            // thumbnail exists in cache
            if (thumbnail_cache.Test(file.FullName)) {
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Cache hit for {file.Name}", $"THUMB:{thread_id}", thread_id);

            // build new thumbnail for an image and add it to the cache
            } else {
                try {
                    BuildCache(file, mime_type, thread_id, thumbnail_cache.MaxAge, out _);
                } catch (Exception ex) {
                    Logging.ErrorAndThrow($"Thumbnail cache build failed :: {file.Name} :: {ex.Message}");
                }
            }

            try {
                if (thumbnail_cache.Test(file.FullName)) {
                    var thumbnail = thumbnail_cache.Request(file.FullName).data;
                    context.Response.ContentType = thumbnail_cache.Request(file.FullName).mime;
                    context.Response.ContentLength64 = thumbnail.LongLength;
                    context.Response.SendChunked = false;
                    context.Response.AddHeader("Accept-Ranges", "none");
                } else goto cache_fail;

                using (MemoryStream ms = new MemoryStream(thumbnail_cache.Request(file.FullName).data, false)) {
                    await ms.CopyToAsync(context.Response.OutputStream, State.cancellation_token).ContinueWith(r => {
                        // success
                        Send.OK(context);
                        if (State.LogLevel == Logging.LogLevel.ALL)
                            Logging.ThreadMessage($"Sent thumb: {file.Name}", $"THUMB:{thread_id}", thread_id);
                    });                
                }
            }
            catch (HttpListenerException ex)
            {
                Logging.Error($"{file.Name} :: {ex.Message}");
            }

        }
    }
}
