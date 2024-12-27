using ShareHole;
using Microsoft.Data.Sqlite;
using ShareHole.Configuration;
using TagLib;
using HeyRed.Mime;
using ChoonGang;

namespace ChoonGang {
    public class MusicFile {
        public string title, artist, album,  path;
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
            this.path = path;
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

        public static void FromDB() {

        }

        public string ToValuesString() {
            return $"'{path}', '{title}', '{artist}', '{album}', {track_number}, {year}, {seconds}";
        }
    }

    public class MusicDB {
        public static string music_root => State.server["server"]["path"].ToString();

        SqliteConnection connection;

        string connection_string = "Data Source=music.db";

        const string create_music_table_query = @"
                    CREATE TABLE IF NOT EXISTS music (
                        path TEXT PRIMARY KEY,
                        title TEXT,
                        artist TEXT,
                        album TEXT,
                        track_number INTEGER DEFAULT 0,
                        year INTEGER DEFAULT 0,
                        duration REAL DEFAULT 0
                    );
            ";

        const string add_song_query = 
            "INSERT OR IGNORE INTO music (path, title, artist, album, track_number, year, duration) " +
                                 "VALUES (@path, @title, @artist, @album, @track_number, @year, @duration);";

        void add_songs(List<MusicFile> files) {
            lock (connection) {
                using (var transaction = connection.BeginTransaction()) {
                    using (var command = new SqliteCommand(add_song_query, connection, transaction)) {
                        foreach (var file in files) {
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("@path", file.path);
                            command.Parameters.AddWithValue("@title", file.title ?? string.Empty);
                            command.Parameters.AddWithValue("@artist", file.artist ?? string.Empty);
                            command.Parameters.AddWithValue("@album", file.album ?? string.Empty);

                            command.Parameters.AddWithValue("@track_number", file.track_number);
                            command.Parameters.AddWithValue("@year", file.year);
                            command.Parameters.AddWithValue("@duration", file.seconds);

                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public MusicDB(ConfigWithExpectedValues config) {
            connection = new SqliteConnection(connection_string);

            connection.Open();

            //create tables
            using (var command = new SqliteCommand(create_music_table_query, connection)) {
                command.ExecuteNonQuery();
            }

            int song_count = 0;
            int corrupt_count = 0;
            var di = new DirectoryInfo(config["server"]["path"].ToString());

            //recurse music directory and add all files
            if (di.Exists) {
                DateTime start = DateTime.Now;

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
                                } finally {
                                    song_count++;                                    
                                }
                            }
                        }

                        if (songs_in_folder.Count > 0) add_songs(songs_in_folder);
                    });
                }

                while (Tasks.TaskCount > 0) {
                    Console.Title = $"Adding music to database: {song_count} song{(song_count != 1 ? "s" : "")}, found {corrupt_count} corrupt song{(corrupt_count != 1 ? "s" : "")}";
                    Thread.Sleep(10);
                }

                DateTime end = DateTime.Now;
                Logging.Message($"Finished adding {song_count} songs in {(end - start).ToString(@"mm\:ss\.ff")}");
                if (corrupt_count > 0) Logging.Warning($"Found {corrupt_count} corrupt audio files");

            } else Environment.Exit(0);
            
        }
    }
}
