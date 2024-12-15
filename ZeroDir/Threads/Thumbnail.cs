using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using ImageMagick;

namespace ZeroDir.DBThreads {
    public class ThumbnailRequest {
        public FileInfo file;
        public byte[] thumbnail = null;

        public string mime_type = "";
        
        public bool thumbnail_ready = false;
        public bool thread_dispatched = false;

        public int thread_id = 0;

        public HttpListenerResponse response;

        public FolderServer parent_server;
        
        public ThumbnailRequest(FileInfo file, HttpListenerResponse response, FolderServer parent_server, string mime_type, int thread_id) {
            this.file = file;
            this.response = response;
            this.parent_server = parent_server;
            this.thread_id = thread_id;
            this.mime_type = mime_type;
        }
    }

    public static class ThumbnailManager {
        static int thumbnail_size = 192;

        //cache for thumbnails which have been loaded at least once
        static volatile Dictionary<string, (string mime, byte[] data)> thumbnail_cache = new Dictionary<string, (string mime, byte[] data)>();

        static int compression_quality => CurrentConfig.server["gallery"]["thumbnail_compression_quality"].get_int();

        public static void RequestThumbnail(string filename, HttpListenerResponse response, FolderServer parent_server, string mime_type, int thread_id) {
            FileInfo f = new FileInfo(filename);
            if (f.Exists) {
                RequestThumbnail(f, response, parent_server, mime_type, thread_id);
            } else return;
        }

        public static void RequestThumbnail(FileInfo file, HttpListenerResponse response, FolderServer parent_server, string mime_type, int thread_id) {
            var tr = new ThumbnailRequest(file, response, parent_server, mime_type, thread_id);

            Task.Run(() => { build_thumbnail(tr); }, CurrentConfig.cancellation_token);

            //Task bt = new Task(build_thumbnail, CurrentConfig.cancellation_token);
        }

        static byte[] get_first_video_frame_from_ffmpeg(ThumbnailRequest request) {
            byte[] output;

            using (var stream_output = new MemoryStream()) {
                var stream_video = FFMpegArguments
                    .FromFileInput(request.file)
                    .OutputToPipe(new StreamPipeSink(stream_output), options =>
                        options.WithFrameOutputCount(1)
                        .WithVideoCodec(VideoCodec.Png)
                        .Resize(thumbnail_size, thumbnail_size)
                        .ForceFormat("image2pipe")
                        )
                    .ProcessSynchronously();

                output = stream_output.ToArray();
            }

            return output;
        }


        static void convert_to_jpeg(ref MagickImage image) {
            image.Settings.Format = MagickFormat.Jpg;
            image.Settings.Compression = CompressionMethod.JPEG;
            image.Quality = (uint)compression_quality;
        }

        static async void build_thumbnail(object data) {
            var request = (ThumbnailRequest)data;

            thumbnail_size = CurrentConfig.server["gallery"]["thumbnail_size"].get_int();

            //cache hit, do nothing
            if (thumbnail_cache.ContainsKey(request.file.FullName)) {
                if (Logging.CurrentLogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Cache hit for {request.file.Name}", $"THUMB:{request.thread_id}", request.thread_id);

            //build new thumbnail for an image and add it to the cache
            } else if (request.mime_type.StartsWith("image")) {
                if (Logging.CurrentLogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Building thumbnail for image {request.file.Name}", $"THUMB:{request.thread_id}", request.thread_id);

                MagickImage mi = new MagickImage(request.file.FullName);

                convert_to_jpeg(ref mi);

                mi.Resize((uint)thumbnail_size, (uint)thumbnail_size);

                try {
                    lock (thumbnail_cache) thumbnail_cache.Add(request.file.FullName, ("image/jpeg", mi.ToByteArray()));
                } catch (Exception ex) {
                    Logging.ErrorAndThrow($"{request.file.Name} :: {ex.Message}");
                }

            //build one for a video
            } else if (request.mime_type.StartsWith("video")) {         
                if (Logging.CurrentLogLevel == Logging.LogLevel.ALL)
                    Logging.ThreadMessage($"Building thumbnail for video {request.file.Name}", $"THUMB:{request.thread_id}", request.thread_id);

                var ffmpeg_thumb = get_first_video_frame_from_ffmpeg(request);
                byte[] jpeg_data;

                using (MemoryStream ms = new MemoryStream(ffmpeg_thumb)) {
                    MagickImage mi = new MagickImage(ms);
                    convert_to_jpeg(ref mi);
                    jpeg_data = mi.ToByteArray();
                }

                try {
                    lock (thumbnail_cache) thumbnail_cache.Add(request.file.FullName, ("image/jpeg", jpeg_data));
                } catch (Exception ex) {
                    Logging.ErrorAndThrow($"{request.file.Name} :: {ex.Message}");
                }
            }

            //pull byte array from the cache and set up a few requirements
            request.thumbnail = thumbnail_cache[request.file.FullName].data;
            request.response.ContentType = thumbnail_cache[request.file.FullName].mime;
            request.response.ContentLength64 = request.thumbnail.LongLength;

            try {
                MemoryStream ms = new MemoryStream(request.thumbnail, false);
                var t = ms.CopyToAsync(request.response.OutputStream, CurrentConfig.cancellation_token).ContinueWith(r => {
                    //success
                    request.response.StatusCode = (int)HttpStatusCode.OK;
                    request.response.StatusDescription = "400 OK";
                    request.response.OutputStream.Close();
                    request.response.Close();

                    ms.Close();
                    ms.Dispose();

                    if (Logging.CurrentLogLevel == Logging.LogLevel.ALL)
                        Logging.ThreadMessage($"Sent thumb: {request.file.Name}", $"THUMB:{request.thread_id}", request.thread_id);
                });

            } catch (HttpListenerException ex) {
                Logging.Error($"{request.file.Name} :: {ex.Message}");
            }

        }
    }
}
