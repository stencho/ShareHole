using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace ZeroDir {
    internal static class FileListing {
        public static string BuildListing(string directory) {
            string result = "";
            int file_count;
            List<string> listing = new List<string>();

            DirectoryInfo dirInfo = new DirectoryInfo(directory);

            if (!dirInfo.Exists) return "";

            var directories = dirInfo.GetDirectories();
            var files = dirInfo.GetFiles();
            var url_path = directory.Replace(Server.config.options["server"]["folder"].ToString(), "");
            if (!url_path.StartsWith('/'))
                url_path = url_path.Insert(0, "/");
            url_path = url_path.TrimEnd('/');
            var last_slash = url_path.LastIndexOf('/')+1;
            string up_dir = url_path.Remove(last_slash, url_path.Length - last_slash);

            Logging.Warning(directory);
            Logging.Warning(up_dir);
            Logging.Warning(url_path);

            if (up_dir.Length > 0)
                result += $"<p><a href=\"{up_dir}\">.. [ {up_dir} ])</a></p>";
            else
                result += $"<p><a href=\"{up_dir}\">/</a></p>";

            foreach (var dir in directories) {
                listing.Add($"{dir.Name}");
                result += $"<p><a href=\"{Server.config.options["server"]["URL"]}{url_path}/{dir.Name}\">{dir.Name}</a></p>";
            }
            foreach (var file in files) {
                listing.Add($"{file.Name}");
                result += $"<p><a href=\"{Server.config.options["server"]["URL"]}{url_path}/{file.Name}\">{file.Name}</a></p>";
            }

            return result;
        }
    }
}
