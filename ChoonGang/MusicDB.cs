using ShareHole;
using Microsoft.Data.Sqlite;
using ShareHole.Configuration;
using TagLib;
using HeyRed.Mime;
using ChoonGang;
using System.Text;
using System.Diagnostics.Eventing.Reader;

namespace ChoonGang {
    public struct MusicFile {
        public string title, artist, album, path;
        public int track_number, year;
        public double seconds;

        public string directory => Path.GetDirectoryName(path);

        public MusicFile(string path) {
                using (TagLib.File f = TagLib.File.Create(path)) {
                    title = f.Tag.Title;
                    artist = f.Tag.FirstArtist;
                    album = f.Tag.Album;
                    track_number = (int)f.Tag.Track;
                    year = (int)f.Tag.Year;
                    seconds = f.Properties.Duration.TotalSeconds;
                }

            string root = MusicDB.music_root;
            while (root.EndsWith(Path.DirectorySeparatorChar)) {
                root = root.Remove(root.Length - 1);
            }

            this.path = path.Remove(0, root.Length);
            while (this.path.StartsWith(Path.DirectorySeparatorChar)) {
                this.path = this.path.Remove(0, 1);
            }

            if (Path.DirectorySeparatorChar == '\\') {
                this.path = this.path.Replace('\\', '/');
            }
        }

        public MusicFile(string title, string artist, string album, string path, int track_number, int year, double seconds) {
            this.title = title;
            this.artist = artist;
            this.album = album;
            this.path = path;
            this.track_number = track_number;
            this.year = year;
            this.seconds = seconds;
        }
    }

    public static class MusicDB {
        public static string music_root => State.server["server"]["path"].ToString();
        public static int total_song_count = 0;
        static SqliteConnection connection;

        const string connection_string = "Data Source=music.db";

        const string create_music_table_query = @"
                    CREATE TABLE IF NOT EXISTS music (
                        path TEXT PRIMARY KEY,
                        directory TEXT,
                        filename TEXT,
                        title TEXT,
                        artist TEXT,
                        album TEXT,
                        sort TEXT,
                        track_number INTEGER DEFAULT 0,
                        year INTEGER DEFAULT 0,
                        duration REAL DEFAULT 0
                    );

                    CREATE VIEW IF NOT EXISTS music_sorted AS  
                        SELECT * FROM music ORDER BY sort;

                    CREATE VIEW IF NOT EXISTS music_sorted_reverse AS  
                        SELECT * FROM music ORDER BY sort DESC;
                    
            ";

        const string add_song_query = 
            "INSERT OR IGNORE INTO music (path, directory, title, artist, album, sort, track_number, year, duration) " +
                                 "VALUES (@path, @directory, @title, @artist, @album, @sort, @track_number, @year, @duration);";

        static int add_songs(List<MusicFile> files) {
            int c = 0;
            lock (connection) {
                using (var transaction = connection.BeginTransaction()) {
                    using (var command = new SqliteCommand(add_song_query, connection, transaction)) {
                        foreach (var file in files) {
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("@path", file.path);

                            var dir = Path.GetDirectoryName(file.path);

                            if (Path.DirectorySeparatorChar == '\\') {
                                dir = dir.Replace('\\', '/');                                
                            }

                            command.Parameters.AddWithValue("@filename", Path.GetFileName(file.path));
                            command.Parameters.AddWithValue("@directory", dir);
                            command.Parameters.AddWithValue("@title", file.title ?? string.Empty);
                            command.Parameters.AddWithValue("@artist", file.artist ?? string.Empty);
                            command.Parameters.AddWithValue("@album", file.album ?? string.Empty);

                            command.Parameters.AddWithValue("@track_number", file.track_number);
                            command.Parameters.AddWithValue("@year", file.year);
                            command.Parameters.AddWithValue("@duration", file.seconds);

                            StringBuilder sort_str = new StringBuilder();
                            sort_str.Append(dir + "/");

                            string track_num = file.track_number.ToString();
                            int zero_count = 4 - track_num.Length;
                            if (zero_count < 0) zero_count = 0;
                            while (zero_count > 0) {
                                track_num = "0" + track_num;
                                zero_count--;
                            }
                            sort_str.Append(track_num);
                            if (!string.IsNullOrEmpty(file.title)) sort_str.Append(file.title.Substring(0, 1));
                            sort_str.Append(Path.GetFileName(file.path));

                            command.Parameters.AddWithValue("@sort", sort_str.ToString());

                            c += command.ExecuteNonQuery();                            
                        }
                    }

                    transaction.Commit();
                }
            }
            return c;
        }

        static int prune_missing() {
            int c = 0;            
            var q = "SELECT path FROM music";

            lock (connection) {
                using (var command = new SqliteCommand(q, connection)) {
                    using (var reader = command.ExecuteReader()) {                        
                        while (reader.Read()) {
                            string path = reader.GetString(0);
                            string root = music_root;
                            while (root.EndsWith(Path.DirectorySeparatorChar))
                                root = root.Remove(root.Length - 1);

                            string full_path = root + Path.DirectorySeparatorChar + path;

                            if (!System.IO.File.Exists(full_path)) {
                                c += remove_song(path);
                            } else {
                                total_song_count++;
                            }
                        }
                    }
                }
            }

            return c;
        }

        static int remove_song(string path) {
            int c = 0;
            string q = "DELETE FROM music WHERE path = @path";
            using (var command = new SqliteCommand(q, connection)) {
                command.Parameters.AddWithValue("@path", path);
                c += command.ExecuteNonQuery();
            }
            return c;
        }

        public static string FindNextSong(string path) {
            var q = @"SELECT path 
            FROM music_sorted 
            WHERE sort > (SELECT sort FROM music_sorted WHERE path = @current_path)
            LIMIT 1;";

            using (var command = new SqliteCommand(q, connection)) {
                command.Parameters.AddWithValue("@current_path", path);

                using (var reader = command.ExecuteReader()) {
                    if (reader.Read()) {
                        return reader.GetString(0) ?? "";
                    }
                }
            }

            return "";
        }

        public static string FindPreviousSong(string path) {
            var q = @"
            SELECT path 
            FROM music_sorted_reverse
            WHERE sort < (SELECT sort FROM music_sorted_reverse WHERE path = @current_path)
            LIMIT 1;";

            using (var command = new SqliteCommand(q, connection)) {
                command.Parameters.AddWithValue("@current_path", path);

                using (var reader = command.ExecuteReader()) {
                    if (reader.Read()) {
                        return reader.GetString(0) ?? "";
                    }
                }
            }

            return "";
        }

        public static string GetID3Json() {
            return "";
        }

        public static void Start() {
            connection = new SqliteConnection(connection_string);
            connection.Open();

            //create tables
            using (var command = new SqliteCommand(create_music_table_query, connection)) {
                command.ExecuteNonQuery();
            }

            int added_count = 0;            
            int corrupt_count = 0;
            List<string> corrupt_songs = new List<string>();

            var di = new DirectoryInfo(State.server["server"]["path"].ToString());

            //recurse music directory and add all files
            if (di.Exists) {
                DateTime start = DateTime.Now;

                Tasks.StartTask(() => {
                    List<MusicFile> songs_in_folder = new List<MusicFile>();

                    foreach (var file in di.GetFiles().OrderBy(a => a.Name)) {
                        string mime = ConvertAndParse.GetMimeTypeOrOctet(file.Name);
                        if (mime.StartsWith("audio")) {
                            try {
                                songs_in_folder.Add(new MusicFile(file.FullName));
                            } catch (UnsupportedFormatException ex) {
                            } catch (TagLib.CorruptFileException ex) {
                                Logging.Error($"{file.Name} is corrupt!");
                                corrupt_count++;
                                corrupt_songs.Add(file.FullName);
                            }
                        }
                    }

                    if (songs_in_folder.Count > 0) {
                        added_count += songs_in_folder.Count;
                        add_songs(songs_in_folder);
                    }
                });

                //folder contains other folders
                foreach (var directory in di.GetDirectories("*", new EnumerationOptions() { RecurseSubdirectories = true }).OrderBy(a => a.FullName)) {
                    Tasks.StartTask(() => {
                        List<MusicFile> songs_in_folder = new List<MusicFile>();

                        foreach (var file in directory.GetFiles().OrderBy(a => a.Name)) {
                            string mime = ConvertAndParse.GetMimeTypeOrOctet(file.Name);
                            if (mime.StartsWith("audio")) {
                                try {
                                    songs_in_folder.Add(new MusicFile(file.FullName));
                                } catch (UnsupportedFormatException ex) {
                                } catch (TagLib.CorruptFileException ex) {
                                    Logging.Error($"{file.Name} is corrupt!");
                                    corrupt_count++;
                                    corrupt_songs.Add(file.FullName);
                                }
                            }
                        }

                        if (songs_in_folder.Count > 0) {
                            added_count += songs_in_folder.Count;
                            add_songs(songs_in_folder);
                        }
                    });
                }
                
                while (Tasks.TaskCount > 0) {
                    State.SetTitle($"Adding music to database: {added_count} song{(added_count != 1 ? "s" : "")}, found {corrupt_count} corrupt song{(corrupt_count != 1 ? "s" : "")}");
                    Thread.Sleep(10);
                }

                //done recursing music dir
                DateTime end = DateTime.Now;
                Logging.Message($"Finished adding {added_count} song{(added_count != 1 ? "s" : "")} in {(end - start).ToString(@"mm\:ss\.ff")}");
                if (corrupt_count > 0) Logging.Warning($"Found {corrupt_count} corrupt audio files");

                //prune missing files from database
                start = DateTime.Now;
                State.SetTitle($"pruning database");
                int pruned = prune_missing();
                end = DateTime.Now;
                Logging.Message($"Finished pruning {pruned} file{(pruned != 1 ? "s" : "")} in {(end - start).ToString(@"mm\:ss\.ff")}");

                //done starting up
                State.SetTitle($"serving {total_song_count} songs");

            } else { connection.Close(); Environment.Exit(0); }            
        }

        enum separator_mode { 
            album, directory
        }
        static separator_mode sep_mode = separator_mode.album;


        public static string ListSongsHTML() {
            var sb = new StringBuilder();
            var q = "SELECT path, artist, title, album, track_number, duration, directory FROM music_sorted";

            string sep_prev = "";
            bool sep_first = true;

            using (var command = new SqliteCommand(q, connection)) {
                using (var reader = command.ExecuteReader()) {
                    sb.Append("<div id=\"music-list-container\"><div id='music-list'>");

                    bool need_separator = false;

                    while (reader.Read()) {
                        string path = reader.GetString(0);
                        string artist = reader.GetString(1);
                        string title = reader.GetString(2);
                        string album = reader.GetString(3);

                        int track_num = reader.GetInt32(4);

                        double duration = reader.GetDouble(5);
                        TimeSpan ts = TimeSpan.FromSeconds(duration);

                        string directory = reader.GetString(6);

                        string duration_str = "";
                        if ((int)ts.TotalHours > 0)
                            duration_str += ts.ToString(@"hh\:mm\:ss");
                        else
                            duration_str += ts.ToString(@"mm\:ss");

                        need_separator = false;
                        if (!sep_first) {
                            switch (sep_mode) {
                                case separator_mode.album:
                                    if (album != sep_prev) need_separator = true;
                                    sep_prev = album;
                                    break;
                                case separator_mode.directory:
                                    if (directory != sep_prev) need_separator = true;
                                    sep_prev = directory;
                                    break;
                            }
                        } else {
                            sep_first = false;
                        }

                        if (need_separator) sb.Append($"<hr class='music-list-item-separator'/>");

                        sb.Append(
                            $"<div class='music-list-item'>" +
                            $"<a class='music-list-item-link' href='javascript:void(0)' onclick=\"play_song('{Uri.EscapeDataString(path)}')\"'>" +

                            $"<span class=\"item-outer-span\">" +

                                $"<span class=\"item-inner-span track-num\">" +
                                $"{(track_num > 0 ? track_num : " ")}" + 
                                $"</span>" +

                                $"<span class=\"item-inner-span\">" +
                                $"{artist}" + 
                                $"</span>" +

                                $"<span class=\"item-inner-span\">" +
                                $"{title}" + 
                                $"</span>" +

                                $"<span class=\"item-inner-span\">" +
                                $"{album}" + 
                                $"</span>" +

                                $"<span class=\"item-inner-span duration\">" +
                                $"{duration_str}" + 
                                $"</span>" +

                            $"</span>" +
                            $"</a>" +
                            $"</div>"
                            );

                    }
                    sb.Append("</div></div>");
                }
            }
            

            return sb.ToString();
        }
    }
}
