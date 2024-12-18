using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole.Threads {
    public struct MusicCacheItem {
        string filename;
        long birth;

        string title;
        string artist;
        string album;

        MagickImage cover;
    }

    public class MusicCache {
        Dictionary<string, MusicCacheItem> items = new Dictionary<string, MusicCacheItem>();

        public void Store(string filename, MusicCacheItem item) => items.Add(filename, item); 
        public bool Test(string filename) => items.ContainsKey(filename);
        public void Remove(string filename) => items.Remove(filename);

        public MusicCache() { 
        
        }
    }

    public static class MusicPlayer {
        // MusicDB db;

        public static string music_player_main_view = """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title></title>
                <link rel="stylesheet" href="base.css">
                <style>
                    body {
                        display: block;
                        width:100vw;
                        height:100vh;
                        min-width:100vw;
                        min-height:100vh;
                        max-width:100vw;
                        max-height:100vh;
                        overflow:hidden;
                        margin: 0;
                    }

                    iframe { border: none; }
                                
                    #top {    
                        width: 100%;   
                        height: 130px;
                        display: flex;
                        flex-wrap: wrap;
                        outline-color: rgb(242, 191, 241) !important;
                        outline-width: 1px;
                        outline-style: inset;
                    }

                    #bottom {    
                        overflow:hidden;
                        width: 100%;    
                        height: calc(100vh - 130px);
                        display: flex;
                        flex-wrap: wrap;
                    }

                    #bottom-left {    
                        width: 50%;     
                        outline-color: rgb(242, 191, 241) !important;
                        outline-width: 1px;
                        outline-style: inset;
                    }
                    #bottom-right {    
                        width: 50%;     
                        outline-color: rgb(242, 191, 241) !important;
                        outline-width: 1px;
                        outline-style: inset;
                    }

                    #file-list-frame {
                        width:100%;
                        height: 100%;
                        outline-color: rgb(242, 191, 241) !important;
                        outline-width: 1px;
                        outline-style: inset;
                    }

                    .progress-container {
                        width: 100%;
                        height: 10px;
                        background-color: #e0e0e0;
                        cursor: pointer;
                        padding-bottom:5px
                    }
            
                    .progress-bar {
                        height: 100%;
                        background-color: #007bff;
                        width: 0%;
                    }
            
                    #music-info-container {
                        display: flex;
                        flex-direction: row;
                        justify-content: flex-start;
                        flex-wrap: wrap;
                        height: 100px;
                        width: 50%;
                    }
            
                    #music-info-details {
                        padding-left: 15px;
                    }
            
                    #music-info-title {
                        width: fit-content;
                        height: fit-content;
                    }
            
                    #music-info-artist {
                        width: fit-content;
                        height: fit-content;
                    }
            
                    #music-info-album {
                        width: fit-content;
                        height: fit-content;
                    }
            
                    #music-info-cover {            
                        max-width: 100px;
                        max-height: 100px;
                        width: 100px;
                        height: 100px;
                    }

                    .audio-controls-container {
                        text-align: center;
                        display: flex;
                        flex-direction: column;
                        overflow:hidden;
                        height: 100px;
                        width: 50%;
                    }
            
                    .audio-controls {
                        display: inline;
                        justify-content: space-around;
                        margin-bottom: 10px;
                        height: 100px;
                        width: 50%;
                    }
            
                    .audio-controls button {
                        padding: 10px;
                        background-color: #007bff;
                        border: none;
                        color: white;
                        font-size: 16px;
                        cursor: pointer;
                        border-radius: 5px;
                        transition: background-color 0.3s;
                    }
            
                    .audio-controls button:hover {
                        background-color: #0056b3;
                    }           
            
                    audio-player {
                        display: none;
                    }

                    .track-list {
                        margin-top: 15px;
                        padding: 0;
                        list-style-type: none;
                        text-align: left;
                        max-height: 100vh;
                        overflow-y: auto;
                    }
            
                    .track-list li {
                        margin: 5px 0;
                        cursor: pointer;
                        color: #007bff;
                    }
            
                    .track-list li:hover {
                        text-decoration: underline;
                    }
            
                    .track-list li:selected {
                        color: #ff00ff;                        
                    }

                </style>
            </head>
            <body>
                <audio id="audio-player" preload="auto">
                    <source src="http://localhost:8080/loot/music/50%20Cent/Get%20Rich%20Or%20Die%20Tryin'/50%20Cent%20-%2004%20-%20Many%20Men%20%28Wish%20Death%29.mp3" type="audio/mp3">
                    Your browser does not support the audio element.
                </audio>
            
                <div id="top">
                    <div id="music-info-container">
                        <div id="music-info-cover"></div>
                        <div id="music-info-details">
                            <div id="music-info-title">Title</div>
                            <div id="music-info-artist">The Artists</div>
                            <div id="music-info-album">The Album</div>
                        </div>
                    </div>
                    <div class="audio-controls-containerr">
                        <div class="audio-controls">
                            <button id="play-button">Play</button>
                            <button id="pause-button">Pause</button>
                            <button id="previous-button">Previous</button>
                            <button id="next-button">Next</button>
                        </div>
                    </div>
                    <div class="progress-container" id="progress-container">
                        <div class="progress-bar" id="progress-bar"></div>
                    </div>
                </div>

                <div id="bottom">
                    <div id="bottom-left">
                        <iframe id="file-list-frame" name="file_list_frame" src="{music_player_dir}"></iframe> 
                    </div>
                    <div id="bottom-right">
                        <!-- ADD <li onclick="loadSong('http://{request.UserHostName}/{passdir}/{share_name}{Uri.EscapeDataString(url_path + fi.Name)}')">{fi.Name}</li> -->
                        <ul class="track-list">
                            {track_list}
                        </ul>
                    </div>
                </div>
            </body>
            <script>
                const list_frame = document.getElementById('file-list-frame');  
                const list_frame_window = list_frame.contentWindow ;
            
                const audio_player = document.getElementById('audio-player');
            
                const play_button = document.getElementById('play-button');
                const pause_button = document.getElementById('pause-button');
                const next_button = document.getElementById('next-button');
                const previous_button = document.getElementById('previous-button');
            
                const progress_bar = document.getElementById('progress-bar');
                const progress_container = document.getElementById('progress-container');
            
                const current_file_title = document.getElementById('current-file'); 
            
                let current_index = 0; 
            
                const file_list = [];

                // keep the progress bar updated
                audio_player.addEventListener('timeupdate', () => {
                    const progress = (audio_player.currentTime / audio_player.duration) * 100;
                    progress_bar.style.width = progress + '%';
                });
            
                // update the track progress when the progress bar is clicked
                progress_container.addEventListener('click', (e) => {
                    const offset_x = e.offsetX;
                    const width = progress_container.offsetWidth;
                    const newTime = (offset_x / width) * audio_player.duration;
                    audio_player.currentTime = newTime;
                });                
            
                function update_title() {

                }

                function queue_song(filename) {     
                    player_frame.queue(filename, 'test');
                }

                function change_directory(url) {
                    list_frame.src = url;
                }            
            
                function next() {            
                    current_index++;
                    if (current_index > file_list.length-1) current_index = 0;
                    audio_player.src = file_list[current_index];
                    current_file_title.textContent = current_index + decodeURIComponent(file_list[current_index]); 
                    audio_player.play();
                }
            
                function previous() {
                    current_index--;
                    if (current_index < 0) current_index = file_list.length-1;
                    audio_player.src = file_list[current_index];
                    current_file_title.textContent = current_index + decodeURIComponent(file_list[current_index]); 
                    audio_player.play();
                }
            
                function queue(filename, displayname) {
                    console.log(filename);
                }
            
                play_button.addEventListener('click', () => { audio_player.play(); });
                pause_button.addEventListener('click', () => { audio_player.pause(); });
            
                next_button.addEventListener('click', () => { next(); });
                previous_button.addEventListener('click', () => { previous(); }); 
                    
                // skip to next file when current one ends
                audio_player.addEventListener("ended", () => { next(); });
            
                // load a file and attempt to find it in the list for the sake of highlighting + next/previous track stuff
                function loadSong(filename) {    
                    audio_player.src = filename;
                    current_file_title.textContent = current_index + decodeURIComponent(filename);  // Update the current file title
                    audio_player.play();   
            
                    current_index = file_list.findIndex(function(f) {
                        return decodeURIComponent(filename).endsWith(decodeURIComponent(f)) || filename == decodeURIComponent(f);
                    });
                }
            </script>
            </html>
            """;

    }
}
