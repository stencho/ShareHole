using FFMpegCore;
using HeyRed.Mime;
using ImageMagick;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole
{
    internal static class Renderer {
        struct listing_info {
            public string passdir => State.server["server"]["passdir"].ToString().Trim();
            public string up_dir;
            public string grouping;
            public string[] extensions;
            public string uri_path;

            public bool cares_about_groups;
            public bool using_extensions;
            public bool show_dirs;
            public bool lore_cache => State.server["gallery"]["lore_cache"].ToBool();

            public DirectoryInfo[] directories;
            public FileInfo[] files;

            public bool GetFile(string filename, out FileInfo result) {
                foreach (FileInfo f in files) {
                    if (f.Name.ToLower() == filename.ToLower()) {
                        result = f;
                        return true;
                    }
                }  
                
                result = null;
                return false;
            }
            
        }

        private static ConcurrentCache<string> guide_cache = new ConcurrentCache<string>();
        
        static listing_info get_directory_info(string directory, string prefix, string uri, string share_name) {
            listing_info info = new listing_info();

            info.uri_path = Uri.UnescapeDataString(uri);

            while (info.uri_path.StartsWith('/')) {
                info.uri_path = info.uri_path.Remove(0, 1);
            }

            while (prefix.EndsWith('/')) {
                prefix = prefix.Remove(prefix.Length - 1, 1);
            }

            if (!directory.EndsWith('/')) directory = directory + "/";

            Logging.Custom($"listing directory: {directory}{info.uri_path}", "RENDER][BuildListing", ConsoleColor.DarkYellow);

            DirectoryInfo dirInfo = new DirectoryInfo($"{directory}{info.uri_path}");
            if (!dirInfo.Exists) Logging.ErrorAndThrow($"Directory {Path.Combine(directory,info.uri_path)} does not exist");

            info.directories = dirInfo.GetDirectories();
            info.files = dirInfo.GetFiles();

            info.up_dir = info.uri_path;
            if (info.up_dir.EndsWith("/")) info.up_dir = info.up_dir.Remove(info.up_dir.Length - 1);
            int slash_i = info.up_dir.LastIndexOf('/');
            if (slash_i > -1) info.up_dir = info.up_dir.Remove(slash_i);
            else info.up_dir = "";

            Logging.Custom($"up_dir: {info.up_dir}", "RENDER][BuildListing", ConsoleColor.DarkYellow);

            info.show_dirs = true;
            if (State.shares[share_name].ContainsKey("show_directories")) {
                info.show_dirs = State.shares[share_name]["show_directories"].ToBool();
            }

            info.grouping = "";
            info.cares_about_groups = false;
            if (State.shares[share_name].ContainsKey("group_by")) {
                info.cares_about_groups = true;
                info.grouping = State.shares[share_name]["group_by"].ToString();
                if (info.grouping.Trim().ToLower() != "type" && info.grouping.Trim().ToLower() != "extension" && info.grouping.Trim().ToLower() != "none") {
                    info.cares_about_groups = false;
                    info.grouping = "none";
                }
            }
            info.grouping = info.grouping.Trim().ToLower();

            info.using_extensions = false;
            info.extensions = null;
            if (State.shares[share_name].ContainsKey("extensions")) {
                info.extensions = State.shares[share_name]["extensions"].ToString().Trim().ToLower().Split(" ");
                info.using_extensions = true;
                for (int i = 0; i < info.extensions.Length; i++) {
                    info.extensions[i] = info.extensions[i].Trim();
                    info.extensions[i] = info.extensions[i].Replace(".", "");
                }
            }

            return info;
        }

        static string build_mp4_stream_tag(string mime, string prefix, listing_info info, string share, string uri, FileInfo file) {
            string converters = "";
            if (mime.StartsWith("video") && State.server["list"]["show_stream_button"].ToBool()) {
                converters += $"⸢<text class=\"converter-text\">" +
                    $"<a href=\"http://{prefix}/{info.passdir}/transcode/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">" +
                    $"⯈MP4" +
                    $"</a>" +
                    $"</text>⸥";
            }
            return converters;
        }
        static string build_image_convert_tag(string mime, string prefix, listing_info info, string share, string uri, FileInfo file) {
            string converters = "";
            string conversion = ConvertAndParse.CheckConversion(mime, true, false, false);

            if (!string.IsNullOrEmpty(conversion) && State.server["list"]["show_convert_image_buttons"].ToBool()) {
                converters += $"⸢<text class=\"converter-text\">" +
                    $"<a href=\"http://{prefix}/{info.passdir}/to_png/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">PNG</a>" +
                    $"/" +
                    $"<a href=\"http://{prefix}/{info.passdir}/to_jpg/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">JPG</a>" +
                    $"</text>⸥ ";
            }
            return converters;
        }
        static string build_audio_convert_tag(string mime, string prefix, listing_info info, string share, string uri, FileInfo file) {
            string converters = "";
            string conversion = ConvertAndParse.CheckConversion(mime, true, false, false);

            if (!string.IsNullOrEmpty(conversion) && State.server["list"]["show_convert_image_buttons"].ToBool()) {
                converters += $"⸢<text class=\"converter-text\">" +
                    $"<a href=\"http://{prefix}/{info.passdir}/to_png/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">PNG</a>" +
                    $"/" +
                    $"<a href=\"http://{prefix}/{info.passdir}/to_jpg/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">JPG</a>" +
                    $"</text>⸥ ";
            }
            return converters;
        }

        public static string FileListing(string directory, string prefix, string uri_path, string share_name) {
            listing_info info;
            try {
                info = get_directory_info(directory, prefix, uri_path, share_name);
                
            } catch (Exception ex) {
                Logging.Error($"Ex: {ex.Message}");
                return"ERROR";
            }

            //TODO: hide hidden files/folders + allow forcing them to show through an option
            int file_count;
            string result = "";

            int dir_c = 0;
            int file_c = 0;

            var share = share_name.Trim();
            var uri = uri_path.Trim();

            while (share.EndsWith("/")) share = share.Remove(share.Length - 1);            
            while (share.StartsWith("/")) share = share.Remove(0, 1);

            while (uri.EndsWith("/")) uri = uri.Remove(uri.Length - 1);
            while (uri.StartsWith("/")) uri = uri.Remove(0, 1);
            
            if (uri.Length > 0) uri = uri + '/';

            //Add up dir if we're showing directories
            if (info.show_dirs && (uri.Trim() != share.Trim()) && uri.Trim().Length != 0 && uri.Trim() != "/") {
                result += $"" +
                    $"<div class=\"list-item\">" +
                    $"<a class=\"list-item-link\" href=\"http://{prefix}/{info.passdir}/{share}/{info.up_dir}\">" +
                    $"<span class=\"file\">" +
                    $"📁" +
                    $"↑ ⸢/{info.up_dir}⸥" +
                    $"</span>" +
                    $"</a>" +
                    $"</div>";
            }

            // DIRECTORIES
            if (info.show_dirs) {
                if (info.grouping != "none" && info.cares_about_groups && info.directories.Length > 0) 
                    result += $"<p class=\"head\"><b>⸢Directories⸥</b></p>";
                
                foreach (var dir in info.directories.OrderBy(a => a.Name)) {
                    result += $"" +
                        $"<div class=\"list-item\">" +
                        $"<a class=\"list-item-link\" href=\"http://{prefix}/{info.passdir}/{share}/{uri}{Uri.EscapeDataString($"{dir.Name}")}\">" +
                        $"<span class=\"file\">" +
                        $"📁" +
                        $"{dir.Name}" +
                        $"</span>" +
                        $"</a>" +
                        $"</div>";
                    dir_c++;
                }
            }
            string auto_conversion = "";
            string converters = "";

            // FILES
            if (info.grouping == "none" || !info.cares_about_groups) {
                foreach (var file in info.files.OrderBy(a => a.Name)) {
                    var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                    var mime = ConvertAndParse.GetMimeTypeOrOctet(file.Name);

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;

                    auto_conversion = ConvertAndParse.CheckConversionList(mime);

                    if (mime.StartsWith("video")) {
                        converters = build_mp4_stream_tag(mime, prefix, info, share, uri, file);
                    } else if (ConvertAndParse.IsValidImage(mime)) {
                        converters = build_image_convert_tag(mime, prefix, info, share, uri, file);
                    }

                    result += $"" +
                        $"<div class=\"list-item\">" +
                        $"<a class=\"list-item-link\" href=\"http://{prefix}/{info.passdir}{auto_conversion}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">" +
                        $"<span class=\"file\">" +
                        $"{file.Name}" +
                        $"</span>" +
                        $"</a>" +
                        $"<span class=\"converter-container\">{converters}</span>" +
                        $"</div>";
                    file_c++;
                }

            } else if (info.grouping == "extension" && info.cares_about_groups) {
                string previous_ext = "";
                foreach (var file in info.files.OrderBy(x => new FileInfo(x.Name).Extension.Replace(".", ""))) {
                    var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                    var mime = ConvertAndParse.GetMimeTypeOrOctet(file.Name);

                    if (ext != previous_ext) {
                        if ((info.using_extensions && info.extensions.Contains(ext.ToLower())) || !info.using_extensions ) 
                        result += $"<p class=\"head\"><b>⸢{ext}⸥</b></p>\n";
                    }

                    previous_ext = ext;

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;

                    auto_conversion = ConvertAndParse.CheckConversionList(mime);

                    if (mime.StartsWith("video")) {
                        converters = build_mp4_stream_tag(mime, prefix, info, share, uri, file);
                    } else if (ConvertAndParse.IsValidImage(mime)) {
                        converters = build_image_convert_tag(mime, prefix, info, share, uri, file);
                    }

                    result += $"" +
                        $"<div class=\"list-item\">" +
                        $"<a class=\"list-item-link\" href=\"http://{prefix}/{info.passdir}{auto_conversion}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">" +
                        $"<span class=\"file\">" +
                        $"{file.Name}" +
                        $"</span>" +
                        $"</a>" +
                        $"<span class=\"converter-container\">{converters}</span>" +
                        $"</div>";
                    file_c++;
                }

            } else if (info.grouping == "type" && info.cares_about_groups) {
                string previous_mime = "";
                string current_type = "";

                foreach (var file in info.files.OrderBy(x => ConvertAndParse.GetMimeTypeOrOctetMinusExt(x.Name)).ThenBy(x => x.Name)) {                    
                    var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                    var mime = ConvertAndParse.GetMimeTypeOrOctet(file.Name);

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower()))
                        continue;

                    if (mime != previous_mime) {
                        var slashi = mime.IndexOf("/");
                        var t = mime.Substring(0, slashi);
                        if (current_type != t) {
                            result += $"<p class=\"head\"><b>⸢{t}⸥</b></p>\n";
                            current_type = t;
                        }
                    }
                
                    previous_mime = mime;

                    auto_conversion = ConvertAndParse.CheckConversionList(mime);

                    if (mime.StartsWith("video")) {
                        converters = build_mp4_stream_tag(mime, prefix, info, share, uri, file);
                    } else if (ConvertAndParse.IsValidImage(mime)) {
                        converters = build_image_convert_tag(mime, prefix, info, share, uri, file);
                    }

                    result += $"" +
                        $"<div class=\"list-item\">" +
                        $"<a class=\"list-item-link\" href=\"http://{prefix}/{info.passdir}{auto_conversion}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">" +
                        $"<span class=\"file\">" +
                        $"{file.Name}" +
                        $"</span>" +
                        $"</a>" +
                        $"<span class=\"converter-container\">{converters}</span>" +
                        $"</div>";
                    file_c++;
                }
            }

            Logging.Custom($"listed {dir_c} folders and {file_c} files", "RENDER][FileListing", ConsoleColor.DarkYellow);

            return result;
        }

        private static void draw_guide() {
            
        }
        
        //IMAGE/VIDEO GALLERY
        public static string Gallery(string directory, string prefix, string uri_path, string share_name) {
            listing_info info;
            try {
                info = get_directory_info(directory, prefix, uri_path, share_name);
                
            } catch (Exception ex) {
                Logging.Error($"Ex: {ex.Message}");
                return"ERROR";
            }
            
            int dir_c = 0;
            int file_c = 0;

            var share = share_name.Trim();
            var uri = uri_path.Trim();
            
            while (share.EndsWith("/")) share = share.Remove(share.Length - 1);
            while (share.StartsWith("/")) share = share.Remove(0, 1);

            while (uri.EndsWith("/")) uri = uri.Remove(uri.Length - 1);
            while (uri.StartsWith("/")) uri = uri.Remove(0, 1);

            if (uri.Length > 0) uri = uri + '/';
            
            Logging.Custom($"rendering gallery for [share] {share}", "RENDER][Gallery", ConsoleColor.Magenta);
            
            string result = "";
            if (!info.lore_cache) {
                if (info.GetFile("lore.html", out var fi)) {
                    var header_guide = fi.OpenText().ReadToEnd();
                    
                    result += "<div id=\"guide\">";
                    result += $"<p>{header_guide}</p>";
                    result += "</div>";
                }
            } else {
                //write guide to cache
                if (uri_path == "/") {
                    string guide_uri = $"{directory}{uri_path}/lore.html";

                    new_cache:
                    if (!guide_cache.Test(guide_uri)) {
                        if (info.GetFile("lore.html", out var fi)) {
                            Logging.Custom($"Caching guide for [share] {share}", "RENDER][Gallery",
                                ConsoleColor.Magenta);
                            guide_cache.Store(guide_uri, fi.OpenText().ReadToEnd());
                            goto new_cache;
                        }
                    } else {
                        string header_guide = guide_cache.Request(guide_uri);
                        result += "<div id=\"guide\">";
                        result += $"<p>{header_guide}</p>";
                        result += "</div>";
                    }
                }
            }

            
            result += "<div id=\"gallery\">";
            //Add up dir if we're showing directories
            
            if (info.show_dirs && (uri.Trim() != share.Trim()) && uri.Trim().Length != 0 && uri.Trim() != "/") {
                result +=
                    $"<a href=\"http://{prefix}/{info.passdir}/{share}/{info.up_dir}\">" +
                    $"<span class=\"thumbnail\" >" +
                    $"<font size={State.server["gallery"]["thumbnail_size"].ToInt()}px>" +
                    $"<text class=\"gallery_folder\">⮝</text>" +
                    $"</font>" +
                    $"<br>" +
                    $"<text class=\"gallery_folder_text\">/{info.up_dir}</text>" +
                    $"</span>" +
                    $"</a>" +
                    $"\n";
            }

            if (info.show_dirs) {
                foreach (var dir in info.directories.OrderBy(a => a.Name)) {
                    result +=
                        $"<a href=\"http://{prefix}/{info.passdir}/{share}/{uri}{Uri.EscapeDataString($"{dir.Name}")}\">" +
                        $"<span class=\"thumbnail\" >" +
                        $"<font size={State.server["gallery"]["thumbnail_size"].ToInt()}px>" +
                        $"<text class=\"gallery_folder\">📁</text>" +
                        $"</font>" +
                        $"<br>" +
                        $"<text class=\"gallery_folder_text\">{dir.Name}</text>" +
                        $"</span>" +
                        $"</a>" +
                        $"\n";
                }
            }

            IOrderedEnumerable<FileInfo> files;

            bool group_by_type = false;
            bool group_by_ext  = false;
            if (info.grouping == "type") {
                files = info.files.OrderBy(x => ConvertAndParse.GetMimeTypeOrOctetMinusExt(x.Name)).ThenBy(x => x.Name);
                group_by_type = true;
            } else if (info.grouping == "extension") {
                files = info.files.OrderBy(x => x.Extension.Replace(".","")).ThenBy(x => x.Name);
                group_by_ext = true;
            } else {
                files = info.files.OrderBy(a => a.Name);
            }

            string previous_ext = "";
            string previous_mime = "";
            string current_type = "";

            foreach (var file in files) {            
                var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                var mime = ConvertAndParse.GetMimeTypeOrOctet(file.Name);
                
                string auto_conversion = "";
                bool raw = false;

                if (mime.StartsWith("image") || mime.StartsWith("video") || mime == "application/postscript" || mime == "application/pdf") {
                    //get thumbnail from gallery DB thread
                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower()))
                        continue;

                    auto_conversion = ConvertAndParse.CheckConversionGallery(mime);

                    if (group_by_type && mime != previous_mime) {
                        var slashi = mime.IndexOf("/");
                        var t = mime.Substring(0, slashi);
                        if (current_type != t) {
                            result += $"<p class=\"head\"><b>{t}</b></p>\n";
                            current_type = t;
                        }
                    }

                    if (group_by_ext && ext != previous_ext) {
                        result += $"<p class=\"head\"><b>{ext}</b></p>\n";                        
                    }

                    previous_mime = mime;
                    previous_ext = ext;

                    result +=
                        $"<a href=\"http://{prefix}/{info.passdir}{auto_conversion}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">" +
                        $"<span class=\"thumbnail\" >" +
                        $"<img align=center src=\"http://{prefix}/{info.passdir}/thumbnail/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\"/>" +
                        $"</span>" +
                        $"</a>\n";

                    file_c++;
                } 
            }
            result += "</div>";
            Logging.Custom($"listed {dir_c} folders and {file_c} files", "RENDER][Gallery", ConsoleColor.Magenta);

            return result;
        }

        public static string MusicPlayerDirectoryView(string directory, string prefix, string uri_path, string share_name) {    
            listing_info info;
            try {
                info = get_directory_info(directory, prefix, uri_path, share_name);
                
            } catch (Exception ex) {
                Logging.Error($"Ex: {ex.Message}");
                return"ERROR";
            }
            string listing = "";

            //TODO: hide hidden files/folders + allow forcing them to show through an option
            int file_count;

            int dir_c = 0;
            int file_c = 0;

            var share = share_name.Trim();
            var uri = uri_path.Trim();

            while (uri.EndsWith("#")) uri = uri.Remove(uri.Length - 1);

            while (share.EndsWith("/")) share = share.Remove(share.Length - 1);
            while (share.StartsWith("/")) share = share.Remove(0, 1);

            while (uri.EndsWith("/")) uri = uri.Remove(uri.Length - 1);
            while (uri.StartsWith("/")) uri = uri.Remove(0, 1);

            while (uri.EndsWith("#")) uri = uri.Remove(uri.Length - 1);

            if (uri.Length > 0) uri = uri + '/';

            listing += $"<div id=\"music-list-container\"><div id=\"music-list\">";            

            //Add up dir if we're showing directories
            if (info.show_dirs && (uri.Trim() != share.Trim()) && uri.Trim().Length != 0 && uri.Trim() != "/") {
                listing += $"" +
                    $"<a class=\"list-item-link\" href=\"javascript:void(0)\" onclick=\"change_directory('http://{prefix}/{info.passdir}/music_player_dir/{share}/{Uri.EscapeUriString(info.up_dir).Replace("'", "\\'")}')\">" +
                    $"<div class=\"music-list-item\">" +
                    $"<span class=\"file\">" +
                    $"📁" +
                    $"↑ ⸢/{info.up_dir}⸥" +
                    $"</span>" +
                    $"</div>" +
                    $"</a>";
            }

            // DIRECTORIES
            if (info.show_dirs) {
                foreach (var dir in info.directories.OrderBy(a => a.Name)) {
                    listing += $"" +
                        $"<div class=\"music-list-item\">" +
                        $"<a class=\"list-item-link\" href=\"javascript:void(0)\" onclick=\"change_directory('http://{prefix}/{info.passdir}/music_player_dir/{share}/{Uri.EscapeUriString(uri + dir.Name).Replace("'", "\\'")}')\">" +
                        $"<span class=\"file\">" +
                        $"📁" + 
                        $"{dir.Name}" +
                        $"</span>" +
                        $"</a>" +
                        $"</div>";
                    dir_c++;
                }
            }

            string auto_conversion = "";
            string converters = "";

            // FILES
            foreach (var file in info.files.OrderBy(a => a.Name)) {
                var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                var mime = ConvertAndParse.GetMimeTypeOrOctet(file.Name);

                //if (!mime.StartsWith("audio") && !mime.StartsWith("video") && !mime.StartsWith("image")) continue;

                if (info.using_extensions && !info.extensions.Contains(ext.ToLower()))
                    continue;

                auto_conversion = ConvertAndParse.CheckConversionList(mime);

                if (mime.StartsWith("video")) {
                    //VID

                } else if (ConvertAndParse.IsValidImage(mime)) {
                    converters = build_image_convert_tag(mime, prefix, info, share, uri, file);
                }

                if (mime.StartsWith("audio"))
                    listing += $"" +
                        $"<div class=\"list-item\">" +
                        $"{converters}" +
                        $"<a class=\"list-item-link\" href=\"javascript:void(0)\" onclick=\"play_song('http://{prefix}/{info.passdir}{auto_conversion}/{share}/{Uri.EscapeDataString(uri + file.Name)}')\">" +
                        $"<span class=\"file\">" +
                        $"{file.Name}" +
                        $"</span>" +

                        $"</a>" + 
                        $"</div>";
                else
                    listing += $"" +
                        $"<div class=\"list-item\">" +
                        $"{converters}" +
                        $"<a class=\"list-item-link\" href=\"javascript:void(0)\" onclick=\"play_song('http://{prefix}/{info.passdir}{auto_conversion}/{share}/{Uri.EscapeDataString(uri + file.Name)}')\">" +
                        $"<span class=\"file\">" +
                        $"{file.Name}" +
                        $"</span>" +
                        $"</a>" +
                        $"</div>";
                file_c++;
            }
            listing += "</div></div>";
            return listing;
        }

        public static string MusicPlayerContent(string directory, string prefix, string uri_path, string share_name) {
            listing_info info;
            try {
                info = get_directory_info(directory, prefix, uri_path, share_name);
                
            } catch (Exception ex) {
                Logging.Error($"Ex: {ex.Message}");
                return"ERROR";
            }

            var share = share_name.Trim();
            var uri = uri_path.Trim();

            while (share.EndsWith("/")) share = share.Remove(share.Length - 1);
            while (share.StartsWith("/")) share = share.Remove(0, 1);

            while (uri.EndsWith("/")) uri = uri.Remove(uri.Length - 1);
            while (uri.StartsWith("/")) uri = uri.Remove(0, 1);

            if (uri.Length > 0) uri = uri + '/';

            var cdc = uri;
            cdc = "/" + cdc;

            while (cdc.EndsWith("/")) cdc = cdc.Remove(cdc.Length - 1);

            var lis = cdc.LastIndexOf("/");
            cdc = cdc.Remove(0, lis+1);

            if (string.IsNullOrEmpty(cdc))
                cdc = share_name;

            string result = MusicPlayerData.music_player_main_view
                .Replace("{stylesheet}", MusicPlayerData.stylesheet)
                .Replace("{music_player_list_dir}", $"http://{prefix}/{info.passdir}/music_player_dir/{share}/{uri}")
                .Replace("{current_directory}", $"http://{prefix}/{info.passdir}/{share}/{uri}")
                .Replace("{current_directory_cleaned}", cdc)
                .Replace("{music_info_url}", $"http://{prefix}/{info.passdir}/music_info/{share}/{uri}")
                .Replace("{prefix}", $"http://{prefix}/")
                .Replace("{prefix_pass}", $"http://{prefix}/{info.passdir}/")
                .Replace("{prefix_pass_info}", $"http://{prefix}/{info.passdir}/music_info/")
                .Replace("{passdir}", $"{info.passdir}")
                .Replace("{share_name}", share_name.Trim());

            return result;
        }

        //public static string MusicInfoContent(string file, string prefix, string uri_path, string share_name) {) {

       // }
    }
}
