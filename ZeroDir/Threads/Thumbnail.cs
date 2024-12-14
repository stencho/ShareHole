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

        public ThumbnailRequest(FileInfo file, HttpListenerResponse response, FolderServer parent_server, string mime_type) {
            this.file = file;
            this.response = response;
            this.parent_server = parent_server;
            this.mime_type = mime_type;
        }
    }

    public static class ThumbnailThreadPool {
        //thread for handling thumbnail requests
        static volatile Thread request_thread = new Thread(handle_requests);

        //threads for building thumbnails
        static volatile Thread[] build_threads = new Thread[build_thread_count];
        static volatile ThumbnailRequest[] current_requests = new ThumbnailRequest[build_thread_count];
        static int build_thread_count = 32;

        static int thumbnail_size = 192;

        //the queue that the dispatcher uses for starting threads
        static volatile Queue<ThumbnailRequest> request_queue = new Queue<ThumbnailRequest>(build_thread_count*4);

        //cache for thumbnails which have been loaded at least once
        static volatile Dictionary<string, (string mime, byte[] data)> thumbnail_cache = new Dictionary<string, (string mime, byte[] data)>();

        public static void Start() {
            build_thread_count = CurrentConfig.server["gallery"]["thumbnail_builder_threads"].get_int();
            thumbnail_size = CurrentConfig.server["gallery"]["thumbnail_size"].get_int();

            current_requests = new ThumbnailRequest[build_thread_count];
            build_threads = new Thread[build_thread_count];

            Logging.ThreadMessage($"Starting dispatcher thread, using {build_thread_count} builder threads", "THUMB", 0);
            request_thread.Start();
        }

        public static void RequestThumbnail(string filename, HttpListenerResponse response, FolderServer parent_server, string mime_type) {
            FileInfo f = new FileInfo(filename);
            if (f.Exists) {
                RequestThumbnail(f, response, parent_server, mime_type);
            } else return;
        }

        public static void RequestThumbnail(FileInfo file, HttpListenerResponse response, FolderServer parent_server, string mime_type) {
            //Logging.ThreadMessage($"Requesting thumbnail for {file.Name}", "THUMB", 0);
            lock (request_queue) {
                request_queue.Enqueue(new ThumbnailRequest(file, response, parent_server, mime_type));
            }
        }

        //main dispatch thread loop
        static void handle_requests() {
            while (true) {
                Thread.Sleep(10);
                while (request_queue.Count > 0) {
                    for (int t = 0; t < current_requests.Length; t++) {
                        if (current_requests[t] == null) {
                            lock (request_queue) {
                                current_requests[t] = request_queue.Dequeue();
                            }
                            
                            current_requests[t].thread_id = t;
                            current_requests[t].thread_dispatched = true;

                            build_threads[t] = new Thread(build_thumbnail);
                            build_threads[t].Start(current_requests[t]);
                            
                            break;
                        }
                    }
                }
            }
        }

        internal static byte[] get_first_video_frame_from_ffmpeg(ThumbnailRequest req) {
            byte[] output;

            using (var stream_output = new MemoryStream()) {
                var stream_video = FFMpegArguments
                    .FromFileInput(req.file)
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

        static void build_thumbnail(object request) {
            ThumbnailRequest req = (ThumbnailRequest)request;

            if (thumbnail_cache.ContainsKey(req.file.FullName)) {
                //cache hit, do nothing
                //Logging.ThreadMessage($"Cache hit for {req.file.Name}", "THUMB", req.thread_id);

            } else if (req.mime_type.StartsWith("image")) {
                //Logging.ThreadMessage($"Building thumbnail for image {req.file.Name}", "THUMB", req.thread_id);
                MagickImage mi = new MagickImage(req.file.FullName);
                mi.Resize((uint)thumbnail_size, (uint)thumbnail_size);

                lock (thumbnail_cache) {
                    thumbnail_cache.Add(req.file.FullName, ("image/bmp", mi.ToByteArray()));
                }

            } else if (req.mime_type.StartsWith("video")) {
                //Logging.ThreadMessage($"Building thumbnail for video {req.file.Name}", "THUMB", req.thread_id);

                var thumb = get_first_video_frame_from_ffmpeg(req);

                lock (thumbnail_cache) {
                    thumbnail_cache.Add(req.file.FullName, ("image/png", thumb));
                }
            }

            req.thumbnail = thumbnail_cache[req.file.FullName].data;
            req.response.ContentType = thumbnail_cache[req.file.FullName].mime;
            req.response.ContentLength64 = req.thumbnail.LongLength;

            req.parent_server.current_sub_thread_count++;
            try {
                req.response.OutputStream.BeginWrite(req.thumbnail, 0, req.thumbnail.Length, result => {
                    req.response.StatusCode = (int)HttpStatusCode.OK;
                    req.response.StatusDescription = "400 OK";
                    req.response.OutputStream.Close();
                    req.response.Close();

                    //Logging.ThreadMessage($"Finished writing thumbnail for {req.file.FullName}", "THUMB", req.thread_id);

                    lock (current_requests) {
                        current_requests[req.thread_id] = null;
                    }

                    req.parent_server.current_sub_thread_count--;
                }, req.response);

            } catch (Exception ex) {
                Logging.Error(ex.Message);
                req.parent_server.current_sub_thread_count--;
            }
        }
    }
}
