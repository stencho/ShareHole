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
        static int thumb_compression_quality => State.server["gallery"]["thumbnail_compression_quality"].ToInt();
        static int thumbnail_size => State.server["gallery"]["thumbnail_size"].ToInt();

        static byte[] get_first_video_frame_from_ffmpeg(FileInfo file, HttpListenerContext context, ShareServer parent_server, string mime_type, int thread_id)
        {
            byte[] output;

            var anal = FFProbe.Analyse(file.FullName);

            double final_x, final_y;

            double aspect = anal.PrimaryVideoStream.Width / (double)anal.PrimaryVideoStream.Height;

            if (aspect >= 0)
            {
                final_x = thumbnail_size * aspect;
                final_y = thumbnail_size;
            }
            else
            {
                final_y = thumbnail_size * aspect;
                final_x = thumbnail_size;
            }

            using (var stream_output = new MemoryStream())
            {
                var stream_video = FFMpegArguments
                    .FromFileInput(file)
                    .OutputToPipe(new StreamPipeSink(stream_output), options =>
                        options.WithFrameOutputCount(1)
                        .WithVideoCodec(VideoCodec.Png)
                        .Resize((int)Math.Round(final_x), (int)Math.Round(final_y))
                        .ForceFormat("image2pipe")
                        )
                    .ProcessSynchronously();

                output = stream_output.ToArray();
            }

            return output;
        }

        public static async void BuildThumbnail(FileInfo file, HttpListenerContext context, ShareServer parent_server, string mime_type, int thread_id) {
        cache_fail:
            

            //cache hit, do nothing
            if (thumbnail_cache.Test(file.FullName))
            {
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Cache hit for {file.Name}", $"THUMB:{thread_id}", thread_id);

                //build new thumbnail for an image and add it to the cache
            }
            else if (mime_type.StartsWith("image") || mime_type == "application/postscript" || mime_type == "application/pdf")
            {
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Building thumbnail for image {file.Name}", $"THUMB:{thread_id}", thread_id);

                using (MagickImage mi = new MagickImage(file.FullName)) {

                    if (mi.Orientation != OrientationType.Undefined)
                        mi.AutoOrient();

                    mi.Resize((uint)thumbnail_size, (uint)thumbnail_size);

                    if (State.server["gallery"]["thumbnail_compression"].ToBool()) {
                        ConvertAndParse.Image.ConvertToJpeg(mi, (uint)thumb_compression_quality);

                        try {
                            thumbnail_cache.Store(file.FullName, ("image/jpeg", mi.ToByteArray()));
                            mi.Dispose();
                        } catch (Exception ex) {
                            Logging.Error($"{file.Name} :: {ex.Message}");
                        }

                    } else {
                        ConvertAndParse.Image.ConvertToPng(mi);

                        try {
                            thumbnail_cache.Store(file.FullName, ("image/png", mi.ToByteArray()));
                            mi.Dispose();
                        } catch (Exception ex) {
                            Logging.Error($"{file.Name} :: {ex.Message}");
                        }
                    }

                }

                //build one for a video
            }
            else if (mime_type.StartsWith("video"))
            {
                if (State.LogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Building thumbnail for video {file.Name}", $"THUMB:{thread_id}", thread_id);

                try
                {
                    var ffmpeg_thumb = get_first_video_frame_from_ffmpeg(file, context, parent_server, mime_type, thread_id);

                    byte[] img_data;

                    using (MemoryStream ms = new MemoryStream(ffmpeg_thumb))
                    {
                        using (MagickImage mi = new MagickImage(ms)) {

                            if (mi.Orientation != OrientationType.Undefined)
                                mi.AutoOrient();

                            if (State.server["gallery"]["thumbnail_compression"].ToBool()) {
                                ConvertAndParse.Image.ConvertToJpeg(mi, (uint)thumb_compression_quality);
                                thumbnail_cache.Store(file.FullName, ("image/jpeg", mi.ToByteArray()));
                                mi.Dispose();
                            } else {
                                ConvertAndParse.Image.ConvertToPng(mi);
                                thumbnail_cache.Store(file.FullName, ("image/png", mi.ToByteArray()));
                                mi.Dispose();
                            }

                        }
                    }

                }
                catch (Exception ex)
                {
                    Logging.Error($"{file.Name} :: {ex.Message}");
                    context.Response.Close();
                }
            }

            context.Response.SendChunked = false;
            context.Response.AddHeader("Accept-Ranges", "none");

            //pull byte array from the cache and set up a few requirements
            if (thumbnail_cache.Test(file.FullName))
            {
                var thumbnail = thumbnail_cache.Request(file.FullName).data;
                context.Response.ContentType = thumbnail_cache.Request(file.FullName).mime;
                context.Response.ContentLength64 = thumbnail.LongLength;
            }
            else goto cache_fail;
            try {
                using (MemoryStream ms = new MemoryStream(thumbnail_cache.Request(file.FullName).data, false)) {
                    await ms.CopyToAsync(context.Response.OutputStream, State.cancellation_token).ContinueWith(r => {
                        //success
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
