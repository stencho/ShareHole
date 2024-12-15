﻿using FFMpegCore;
using HeyRed.Mime;
using ImageMagick;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole {
    internal static class Renderer {
        struct listing_info {
            public string passdir;
            public string up_dir;
            public string grouping;
            public string[] extensions;
            public string uri_path;

            public bool cares_about_groups;
            public bool using_extensions;
            public bool show_dirs;
            
            public DirectoryInfo[] directories;
            public FileInfo[] files;
        }

        static listing_info get_directory_info(string directory, string prefix, string uri, string share_name) {
            listing_info info = new listing_info();

            info.uri_path = Uri.UnescapeDataString(uri);

            while (info.uri_path.StartsWith('/')) {
                info.uri_path = info.uri_path.Remove(0, 1);
            }

            while (prefix.EndsWith('/')) {
                prefix = prefix.Remove(prefix.Length - 1, 1);
            }

            info.passdir = CurrentConfig.server["server"]["passdir"].get_string().Trim();

            if (!directory.EndsWith('/')) directory = directory + "/";

            Logging.Custom($"listing directory: {directory}{info.uri_path}", "RENDER][BuildListing", ConsoleColor.DarkYellow);

            DirectoryInfo dirInfo = new DirectoryInfo($"{directory}{info.uri_path}");
            if (!dirInfo.Exists) Logging.ErrorAndThrow($"Directory {Path.Combine(directory,info.uri_path)} does not exist");

            info.directories = dirInfo.GetDirectories();
            info.files = dirInfo.GetFiles();

            info.up_dir = info.uri_path;
            int slash_i = info.up_dir.LastIndexOf('/');
            if (slash_i > -1) info.up_dir = info.up_dir.Remove(slash_i);
            else info.up_dir = "";

            Logging.Custom($"up_dir: {info.up_dir}", "RENDER][BuildListing", ConsoleColor.DarkYellow);

            info.show_dirs = true;
            if (CurrentConfig.shares[share_name].ContainsKey("show_directories")) {
                info.show_dirs = CurrentConfig.shares[share_name]["show_directories"].get_bool();
            }

            info.grouping = "";
            info.cares_about_groups = false;
            if (CurrentConfig.shares[share_name].ContainsKey("group_by")) {
                info.cares_about_groups = true;
                info.grouping = CurrentConfig.shares[share_name]["group_by"].get_string();
                if (info.grouping.Trim().ToLower() != "type" && info.grouping.Trim().ToLower() != "extension" && info.grouping.Trim().ToLower() != "none") {
                    info.cares_about_groups = false;
                    info.grouping = "none";
                }
            }
            info.grouping = info.grouping.Trim().ToLower();

            info.using_extensions = false;
            info.extensions = null;
            if (CurrentConfig.shares[share_name].ContainsKey("extensions")) {
                info.extensions = CurrentConfig.shares[share_name]["extensions"].ToString().Trim().ToLower().Split(" ");
                info.using_extensions = true;
                for (int i = 0; i < info.extensions.Length; i++) {
                    info.extensions[i] = info.extensions[i].Trim();
                    info.extensions[i] = info.extensions[i].Replace(".", "");
                }
            }

            return info;
        }

        public static string FileListing(string directory, string prefix, string uri_path, string share_name) {
            listing_info info = get_directory_info(directory, prefix, uri_path, share_name);

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
                result += $"<p style=\"up\"><span class=\"emojitint\">📁<a href=\"http://{prefix}/{info.passdir}/{share}/{info.up_dir}\">↑ [/{info.up_dir}]</a></span></p>\n";
            }

            // DIRECTORIES
            if (info.show_dirs) {
                if (info.grouping != "none" && info.cares_about_groups) 
                    result += $"<p class=\"head\"><b>Directories</b></p>\n";
                
                foreach (var dir in info.directories.OrderBy(a => a.Name)) {
                    result += $"<p><span class=\"emojitint\">📁<a href=\"http://{prefix}/{info.passdir}/{share}/{uri}{Uri.EscapeDataString($"{dir.Name}")}\">{dir.Name}</a></span></p>\n";
                    dir_c++;
                }
            }

            string conversion = "";

            // FILES
            if (info.grouping == "none" || !info.cares_about_groups) {
                foreach (var file in info.files.OrderBy(a => a.Name)) {
                    var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                    var mime = GetMimeTypeOrOctet(file.Name);

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;

                    conversion = check_conversion(mime);

                    result += $"<p><a href=\"http://{prefix}/{info.passdir}{conversion}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">{file.Name}</a></p>\n";

                    file_c++;
                }

            } else if (info.grouping == "extension" && info.cares_about_groups) {
                string previous_ext = "";
                foreach (var file in info.files.OrderBy(x => new FileInfo(x.Name).Extension.Replace(".", ""))) {
                    var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                    var mime = GetMimeTypeOrOctet(file.Name);

                    if (ext != previous_ext && info.extensions.Contains(ext.ToLower())) 
                        result += $"<p class=\"head\"><b>{ext}</b></p>\n";                    

                    previous_ext = ext;

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;

                    conversion = check_conversion(mime);

                    result += $"<p><a href=\"http://{prefix}/{info.passdir}{conversion}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">{file.Name}</a></p>\n";
                    file_c++;
                }

            } else if (info.grouping == "type" && info.cares_about_groups) {
                string previous_mime = "";
                string current_type = "";

                foreach (var file in info.files.OrderBy(x => GetMimeTypeOrOctetMinusExt(x.Name)).ThenBy(x => x.Name)) {                    
                    var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                    var mime = GetMimeTypeOrOctet(file.Name);

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower()))
                        continue;

                    conversion = check_conversion(mime);

                    if (mime != previous_mime) {
                        var slashi = mime.IndexOf("/");
                        var t = mime.Substring(0, slashi);
                        if (current_type != t) {
                            result += $"<p class=\"head\"><b>{t}</b></p>\n";
                            current_type = t;
                        }
                    }
                
                    previous_mime = mime;
                    
                    result += $"<p><a href=\"http://{prefix}/{info.passdir}{conversion}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">{file.Name}</a></p>\n";
                    file_c++;
                }
            }

            Logging.Custom($"listed {dir_c} folders and {file_c} files", "RENDER][FileListing", ConsoleColor.DarkYellow);

            return result;
        }

        //IMAGE/VIDEO GALLERY
        public static string Gallery(string directory, string prefix, string uri_path, string share_name) {
            listing_info info = get_directory_info(directory, prefix, uri_path, share_name);

            int dir_c = 0;
            int file_c = 0;

            var share = share_name.Trim();
            var uri = uri_path.Trim();

            while (share.EndsWith("/")) share = share.Remove(share.Length - 1);
            while (share.StartsWith("/")) share = share.Remove(0, 1);

            while (uri.EndsWith("/")) uri = uri.Remove(uri.Length - 1);
            while (uri.StartsWith("/")) uri = uri.Remove(0, 1);

            if (uri.Length > 0) uri = uri + '/';

            string result = "";

            Logging.Custom($"rendering gallery for [share] {share}", "RENDER][Gallery", ConsoleColor.Magenta);

            result += "<div id=\"gallery\">";
            //Add up dir if we're showing directories
            if (info.show_dirs && (uri.Trim() != share.Trim()) && uri.Trim().Length != 0 && uri.Trim() != "/") {
                result +=
                    $"<a href=\"http://{prefix}/{info.passdir}/{share}/{info.up_dir}\">" +
                    $"<span class=\"thumbnail\" >" +
                    $"<font size={CurrentConfig.server["gallery"]["thumbnail_size"].get_int()}px>" +
                    $"<text class=\"emojitint\">⮝</text>" +
                    $"</font>" +
                    $"<br>" +
                    $"<text class=\"galleryfoldertext\">/{info.up_dir}</text>" +
                    $"</span>" +
                    $"</a>" +
                    $"\n";
            }

            foreach (var dir in info.directories.OrderBy(a => a.Name)) {                
                result +=
                    $"<a href=\"http://{prefix}/{info.passdir}/{share}/{uri}{Uri.EscapeDataString($"{dir.Name}")}\">" +
                    $"<span class=\"thumbnail\" >" +
                    $"<font size={CurrentConfig.server["gallery"]["thumbnail_size"].get_int()}px>" +
                    $"<text class=\"emojitint\">📁</text>" +
                    $"</font>" +
                    $"<br>" +
                    $"<text class=\"galleryfoldertext\">{dir.Name}</text>" +
                    $"</span>" +
                    $"</a>" +
                    $"\n";

                Logging.Custom($"{dir}", "RENDER][Gallery", ConsoleColor.Magenta);
            }
                        
            foreach (var file in info.files.OrderBy(a => a.Name)) {            
                var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                var mime = GetMimeTypeOrOctet(file.Name);
                
                string conversion = "";
                bool raw = false;

                if (mime.StartsWith("image") || mime.StartsWith("video")) {
                    //get thumbnail from gallery DB thread
                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower()))
                        continue;

                    conversion = check_conversion(mime);

                    result +=
                        $"<a href=\"http://{prefix}/{info.passdir}{conversion}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">" +
                        $"<span class=\"thumbnail\" >" +
                        $"<img align=center src=\"http://{prefix}/{info.passdir}/thumbnail/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\"/>" +
                        //(is_raw(mime) ? $"<text>RAW</text>" : "") +
                        $"</span>" +
                        $"</a>\n";

                    file_c++;
                } 
            }
            result += "</div>";
            Logging.Custom($"listed {dir_c} folders and {file_c} files", "RENDER][Gallery", ConsoleColor.Magenta);

            return result;
        }

        public static string MusicPlayer(string directory, string prefix, string uri_path, string share_name) {
            listing_info info = get_directory_info(directory, prefix, uri_path, share_name);

            string result = "";
            return result;
        }

        static bool is_raw(string mime) {
            if (mime == "image/dng") return true;
            else if (mime == "image/raw") return true;
            else return false;
        }

        static string check_conversion(string mime) {
            if (mime.StartsWith("image")) {
                //raw formats
                if (mime.EndsWith("/dng")) return "/to_png";
                if (mime.EndsWith("/raw")) return "/to_png";

                //should work without this, doesn't in chome
                if (mime.EndsWith("/avif")) return "/to_png";

                //adobe
                if (mime.EndsWith("/vnd.adobe.photoshop")) return "/to_png";

            } else if (mime.StartsWith("video")) {

            }

            return "";
        }

        public static string GetMimeTypeOrOctet(string fn) {
            string mimetype;

            var fi = new FileInfo(fn);
            if (fi.Extension.ToLower() == ".dng") return "image/dng";
            if (fi.Extension.ToLower() == ".raw") return "image/raw";
            if (fi.Extension.ToLower() == ".avif") return "image/avif";

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
            if (fi.Extension.ToLower() == ".dng") return "image/dng";
            if (fi.Extension.ToLower() == ".raw") return "image/raw";
            if (fi.Extension.ToLower() == ".avif") return "image/avif";

            try {
                mimetype = MimeTypesMap.GetMimeType(fn);
            } catch {
                mimetype = "application/octet-stream";
            }
            return mimetype.Substring(0, mimetype.IndexOf("/"));
        }
    }
}