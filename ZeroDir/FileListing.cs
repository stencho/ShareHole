using HeyRed.Mime;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir {
    internal static class FileListing {
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

            info.passdir = Config.server["server"]["passdir"].get_string().Trim();

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
            if (Config.shares[share_name].ContainsKey("show_directories")) {
                info.show_dirs = Config.shares[share_name]["show_directories"].get_bool();
            }

            info.grouping = "";
            info.cares_about_groups = false;
            if (Config.shares[share_name].ContainsKey("group_by")) {
                info.cares_about_groups = true;
                info.grouping = Config.shares[share_name]["group_by"].get_string();
                if (info.grouping.Trim().ToLower() != "type" && info.grouping.Trim().ToLower() != "extension" && info.grouping.Trim().ToLower() != "none") {
                    info.cares_about_groups = false;
                    info.grouping = "none";
                }
            }
            info.grouping = info.grouping.Trim().ToLower();

            info.using_extensions = false;
            info.extensions = null;
            if (Config.shares[share_name].ContainsKey("extensions")) {
                info.extensions = Config.shares[share_name]["extensions"].ToString().Trim().ToLower().Split(" ");
                info.using_extensions = true;
                for (int i = 0; i < info.extensions.Length; i++) {
                    info.extensions[i] = info.extensions[i].Trim();
                    info.extensions[i] = info.extensions[i].Replace(".", "");
                }
            }

            return info;
        }

        public static string BuildListing(string directory, string prefix, string uri_path, string share_name) {
            listing_info info = get_directory_info(directory, prefix, uri_path, share_name);

            //TODO: hide hidden files/folders + allow forcing them to show through an option
            int file_count;
            string result = "";

            int dir_c = 0;
            int file_c = 0;

            //Add up dir if we're showing directories
            if (info.show_dirs && (uri_path.Trim() != share_name.Trim()) && uri_path.Trim().Length != 0 && uri_path.Trim() != "/") {
                result += $"<p style=\"up\"><span class=\"emojitint\">📁<a href=\"http://{prefix}/{info.passdir}/{share_name}/{info.up_dir}\">↑ [/{info.up_dir}]</a></span></p>\n";
            }

            if (info.show_dirs) {
                if (info.grouping != "none" && info.cares_about_groups) 
                    result += $"<p class=\"head\"><b>Directories</b></p>\n";
                
                foreach (var dir in info.directories.OrderBy(a => a.Name)) {
                    string n = uri_path;
                    while (n.EndsWith('/')) n = n.Remove(n.Length - 1, 1);
                    if (n.Length > 0) n = n.Insert(0, "/");
                    result += $"<p><span class=\"emojitint\">📁<a href=\"http://{prefix}/{info.passdir}/{share_name}{n}/{Uri.EscapeDataString($"{dir.Name}")}\">{dir.Name}</a></span></p>\n";
                    dir_c++;
                }
            }            

            if (info.grouping == "none" || !info.cares_about_groups) {
                foreach (var file in info.files.OrderBy(a => a.Name)) {
                    string n = uri_path;
                    string f = file.Name;

                    var ext = new FileInfo(f).Extension.Replace(".", "");
                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;
                    
                    while (n.EndsWith('/')) n = n.Remove(n.Length - 1, 1);
                    while (f.StartsWith('/')) f = f.Remove(0, 1);
                    if (n.Length > 0) n = n.Insert(0, "/");

                    result += $"<p><a href=\"http://{prefix}/{info.passdir}/{share_name}{n}/{Uri.EscapeDataString($"{f}")}\">{f}</a></p>\n";

                    file_c++;
                }
            } else if (info.grouping == "extension" && info.cares_about_groups) {
                string previous_ext = "";
                foreach (var file in info.files.OrderBy(x => new FileInfo(x.Name).Extension.Replace(".", ""))) {
                    string n = uri_path;
                    string f = file.Name;

                    var ext = new FileInfo(f).Extension.Replace(".", "");
                    if (ext != previous_ext && info.extensions.Contains(ext.ToLower())) 
                        result += $"<p class=\"head\"><b>{ext}</b></p>\n";                    

                    previous_ext = ext;

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;
                    
                    while (n.EndsWith('/')) n = n.Remove(n.Length - 1, 1);
                    while (f.StartsWith('/')) f = f.Remove(0, 1);
                    if (n.Length > 0) n = n.Insert(0, "/");

                    result += $"<p><a href=\"http://{prefix}/{info.passdir}/{share_name}{n}/{Uri.EscapeDataString($"{f}")}\">{f}</a></p>\n";
                    file_c++;
                }

            } else if (info.grouping == "type" && info.cares_about_groups) {
                string previous_mime = "";
                string current_type = "";

                foreach (var file in info.files.OrderBy(x => GetMimeTypeOrOctet(x.Name)).ThenBy(x => x.Name)) {
                    string n = uri_path;
                    string f = file.Name;
                    
                    var ext = new FileInfo(f).Extension.Replace(".", "");
                    var mime = GetMimeTypeOrOctet(file.Name);

                    if (mime != previous_mime) {
                        var slashi = mime.IndexOf("/");
                        var t = mime.Substring(0, slashi);
                        if (current_type != t) {
                            result += $"<p class=\"head\"><b>{t}</b></p>\n";

                            current_type = t;
                        }
                    }
                
                    previous_mime = mime;

                    if (info.using_extensions && !info.extensions.Contains(ext.ToLower())) 
                        continue;
                    
                    while (n.EndsWith('/')) n = n.Remove(n.Length - 1, 1);
                    while (f.StartsWith('/')) f = f.Remove(0, 1);
                    if (n.Length > 0) n = n.Insert(0, "/");

                    result += $"<p><a href=\"http://{prefix}/{info.passdir}/{share_name}{n}/{Uri.EscapeDataString($"{f}")}\">{f}</a></p>\n";
                    file_c++;
                }
            }
            Logging.Custom($"listed {dir_c} folders and {file_c} files", "RENDER][BuildListing", ConsoleColor.DarkYellow);


            return result;
        }

        public static string BuildGallery(string directory, string prefix, string uri_path, string share_name) {
            listing_info info = get_directory_info(directory, prefix, uri_path, share_name);

            string result = "";

            foreach (var file in info.files.OrderBy(a => a.Name)) {
                
            }
            
            
            return "";
        }

        static string GetMimeTypeOrOctet(string fn) {
            string mimetype;
            try {
                mimetype = MimeTypesMap.GetMimeType(fn);
            } catch {
                mimetype = "application/octet-stream";
            }
            return mimetype;
        }
    }
}
