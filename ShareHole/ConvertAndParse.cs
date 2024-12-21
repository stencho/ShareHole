using HeyRed.Mime;
using ImageMagick;
using System.Globalization;

namespace ShareHole {
    public static class ConvertAndParse {
        public const string transcode_url = "/transcode";
        public const string png_url = "/to_png";
        public const string jpg_url = "/to_jpg";

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


        private const string date_fmt_for_range_parse = "ddd, dd MMM yyyy HH:mm:ss 'GMT'";
        public static long ParseDateHeaderToSeconds(string header) {
            if (header == null) return 0;

            DateTime dt;

            DateTime.TryParseExact(
               header,
               date_fmt_for_range_parse,
               CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
               out dt
           );

            return dt.ToFileTimeUtc();
        }

        public static bool IsValidImage(string mime) => 
            mime.StartsWith("image") || mime == "application/postscript" || mime == "application/pdf";

        public static string CheckConversion(string mime, bool images, bool videos, bool audio) {
            if (mime == "application/postscript") return png_url;

            if (mime.StartsWith("image")) {
                if (!images) return "";

                //raw formats
                if (mime.EndsWith("/dng")) return jpg_url;
                if (mime.EndsWith("/raw")) return jpg_url;

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
                //mp4 audio/m4a
                if (mime.EndsWith("mp4")) return transcode_url;

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
    }
}
