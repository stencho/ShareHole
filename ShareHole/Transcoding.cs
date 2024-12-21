using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using System.Net;

namespace ShareHole {
    public class Transcoding {
        public static async void StreamVideoAsMp4Async(FileInfo file, HttpListenerContext context) {
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

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.StatusDescription = "200 OK";

                if (State.server["transcode"]["use_variable_bit_rate"].ToBool()) {
                    State.StartTask(async () => {
                        await FFMpegArguments
                            .FromFileInput(file.FullName)
                            .OutputToPipe(new StreamPipeSink(context.Response.OutputStream), options => options
                                .ForceFormat("mp4")
                                .ForcePixelFormat("yuv420p")
                                .WithVideoCodec("libx264")
                                .WithAudioCodec("aac")

                                .UsingMultithreading(true)
                                .UsingThreads(State.server["transcode"]["threads_per_video_conversion"].ToInt())
                                .WithSpeedPreset(Speed.VeryFast)
                                .WithFastStart()

                                .WithConstantRateFactor(State.server["transcode"]["vbr_quality_factor"].ToInt())

                                .WithCustomArgument("-map_metadata 0")
                                .WithCustomArgument("-loglevel verbose")
                                .WithCustomArgument("-movflags frag_keyframe+empty_moov")
                                .WithCustomArgument("-movflags +faststart")
                                .WithCustomArgument($"-ab 240k")

                            ).ProcessAsynchronously().ContinueWith(t => {
                                Logging.ThreadMessage($"{file.Name} :: Finished sending data", "CONVERT:MP4", tid);
                            }, State.cancellation_token);
                    });

                } else {
                    State.StartTask(async () => {
                    await FFMpegArguments
                    .FromFileInput(file.FullName)
                        .OutputToPipe(new StreamPipeSink(context.Response.OutputStream), options => options
                            .ForceFormat("mp4")
                            .ForcePixelFormat("yuv420p")
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("aac")

                            .UsingMultithreading(true)
                            .UsingThreads(State.server["transcode"]["threads_per_video_conversion"].ToInt())
                            .WithSpeedPreset(Speed.VeryFast)
                            .WithFastStart()

                            .WithVideoBitrate(State.server["transcode"]["cbr_bit_rate"].ToInt() * 1000)

                            .WithCustomArgument("-map_metadata 0")
                            .WithCustomArgument("-loglevel verbose")
                            .WithCustomArgument("-movflags frag_keyframe+empty_moov")
                            .WithCustomArgument("-movflags +faststart")
                            .WithCustomArgument($"-ab 240k")

                        ).ProcessAsynchronously().ContinueWith(t => {
                            Logging.ThreadMessage($"{file.Name} :: Finished sending data", "CONVERT:MP4", tid);
                        }, State.cancellation_token);
                    });
                }
            } catch (Exception ex) {
                Logging.ThreadError($"{file.Name} :: {ex.Message}", "CONVERT:MP4", tid);
            }
            
        }
    }
}
