using HeyRed.Mime;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir {
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

            // FILES
            if (info.grouping == "none" || !info.cares_about_groups) {
                foreach (var file in info.files.OrderBy(a => a.Name)) {
                    var ext = new FileInfo(file.Name).Extension.Replace(".", "");

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;
                    
                    result += $"<p><a href=\"http://{prefix}/{info.passdir}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">{file.Name}</a></p>\n";

                    file_c++;
                }

            } else if (info.grouping == "extension" && info.cares_about_groups) {
                string previous_ext = "";
                foreach (var file in info.files.OrderBy(x => new FileInfo(x.Name).Extension.Replace(".", ""))) {
                    var ext = new FileInfo(file.Name).Extension.Replace(".", "");

                    if (ext != previous_ext && info.extensions.Contains(ext.ToLower())) 
                        result += $"<p class=\"head\"><b>{ext}</b></p>\n";                    

                    previous_ext = ext;

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;
                    
                    result += $"<p><a href=\"http://{prefix}/{info.passdir}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">{file.Name}</a></p>\n";
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

                    if (mime != previous_mime) {
                        var slashi = mime.IndexOf("/");
                        var t = mime.Substring(0, slashi);
                        if (current_type != t) {
                            result += $"<p class=\"head\"><b>{t}</b></p>\n";
                            current_type = t;
                        }
                    }
                
                    previous_mime = mime;
                    
                    result += $"<p><a href=\"http://{prefix}/{info.passdir}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\">{file.Name}</a></p>\n";
                    file_c++;
                }
            }

            Logging.Custom($"listed {dir_c} folders and {file_c} files", "RENDER][FileListing", ConsoleColor.DarkYellow);

            return result;
        }

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

            string result = "";

            Logging.Custom($"rendering gallery for [share] {share}", "RENDER][Gallery", ConsoleColor.Magenta);

            foreach (var file in info.files.OrderBy(a => a.Name)) {
                var ext = new FileInfo(file.Name).Extension.Replace(".", "");
                var mime = GetMimeTypeOrOctet(file.Name);

                if (mime.StartsWith("image")) {
                    //get thumbnail from gallery DB thread
                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower()))
                        continue;

                    result += $"<a href=\"http://{prefix}/{info.passdir}/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\"><img src=\"http://{prefix}/{info.passdir}/thumbnail/{share}/{uri}{Uri.EscapeDataString($"{file.Name}")}\"/></a>\n";

                    file_c++;
                } else if (mime.StartsWith("video")) {
                    //ffmpeg I guess
                }
            }

            Logging.Custom($"listed {dir_c} folders and {file_c} files", "RENDER][Gallery", ConsoleColor.Magenta);

            return result;
        }

        public static string MusicPlayer(string directory, string prefix, string uri_path, string share_name) {
            listing_info info = get_directory_info(directory, prefix, uri_path, share_name);

            string result = "";

            Logging.Custom($"Not implemented!", "RENDER][MusicPlayer", ConsoleColor.Magenta);
            return "Not implemented!";

            foreach (var file in info.files.OrderBy(a => a.Name)) {

            }

            return "";
        }

        public static string GetMimeTypeOrOctet(string fn) {
            string mimetype;
            try {
                mimetype = MimeTypesMap.GetMimeType(fn);
            } catch {
                mimetype = "application/octet-stream";
            }
            return mimetype;
        }
        public static string GetMimeTypeOrOctetMinusExt(string fn) {
            string mimetype;
            try {
                mimetype = MimeTypesMap.GetMimeType(fn);
            } catch {
                mimetype = "application/octet-stream";
            }
            return mimetype.Substring(0, mimetype.IndexOf("/"));
        }
    }
}
