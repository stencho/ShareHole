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
                        display: flex;
                        width: 100vw;
                        height: 100vh;
                        max-width: 100vw;
                        overflow: hidden;
                        margin: 0;
                        flex-wrap: wrap;
                    }

                    iframe { border: none; }                          
                    audio-player { display: none; }
                    

                    /* TOP */

                    #top {    
                        width: 100%;   
                        height: 110px;
                        display: flex;
                        flex-wrap: wrap;

                        border-bottom: solid 2px var(--main-color);

                        background-color: var(--secondary-background-color);
                    }


                    /* INFO PANE */            

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
            
                    
                    /* MEDIA CONTROLS PANE */

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
                        color: var(--background-color);
                    }
            
                    .audio-controls button {
                        padding: 10px;
                        background-color: var(--secondary-color);
                        border: none;
                        font-size: 16px;
                        cursor: pointer;
                        transition: background-color 0.3s;
                    }
            
                    .audio-controls button:hover {
                        background-color: var(--secondary-color-dark);
                    }           
                        
                    /* PROGRESS BAR */

                    .progress-container {
                        width: 100%;
                        height: 10px;
                        background-color: transparent;
                        cursor: pointer;
                        border-top: solid 2px var(--main-color);
                    }
            
                    .progress-bar {
                        height: 100%;
                        background-color: var(--main-color);
                        width: 0%;
                    }
                    
                    
                    /* BOTTOM */

                    #bottom {    
                        overflow:hidden;
                        width: 100%;    
                        display: flex;
                        
                        bottom: 100vh;
                        height: calc(100% - 110px);

                    }


                    /* BOTTOM LEFT */

                    #bottom-left {    
                        width: 50%;     
            
                        border-right: solid 1px var(--main-color);
                        overflow:hidden;

                        margin: 0;
                    }

                    #directory-box {
                        background-color:#101010; 
                        z-index: 9999; 
                        top: 0;
                        left: 0;
                        width: 100%;
                        height: 30px; 
                        text-align: center;
            
                        border-bottom: solid 2px var(--main-color);
                        box-shadow: inset -1px 0 0 var(--main-color);
            
                        overflow:hidden;
                    }
                    
                    #file-list-frame {
                        width:100%;
                        height:calc(100% - 34px); /* 30px for the directory_box + 4 for the two 2px borders */
                    }


                    /* BOTTOM RIGHT */ 

                    #bottom-right {    
                        width: 50%;     
                        height: 100%;    
                        border-left: solid 1px var(--main-color);
                        overflow:hidden;               
                    }

                    .track-list {
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
                            <button id="play-pause-button">Play</button>
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
                        <div id="directory-box">{current_directory_cleaned}</div>
                        <iframe id="file-list-frame" name="file_list_frame" src="{music_player_list_dir}"></iframe> 
                    </div>
                    <div id="bottom-right">
                        <div id="track-list">
                        </div>
                    </div>
                </div>
            </body>

            <script>
                const list_frame = document.getElementById('file-list-frame');  
                const list_frame_window = list_frame.contentWindow ;
            
                const audio_player = document.getElementById('audio-player');
            
                const play_pause_button = document.getElementById('play-pause-button');
                const next_button = document.getElementById('next-button');
                const previous_button = document.getElementById('previous-button');
            
                const progress_bar = document.getElementById('progress-bar');
                const progress_container = document.getElementById('progress-container');
            
                const directory_box = document.getElementById('directory-box'); 

                const track_list = document.getElementById('track-list');
                const file_list = [];

                const share_name = '{share_name}';                

                let current_index = 0; 
            
            
                directory_box.addEventListener('onload', () => {
                    console.log('{current_directory}');
                    directory_box.innerHTML = '{current_directory}';
                    directory_box.innerHTML = 'test';
                });

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

                function change_directory(url) {
                    list_frame.src = url;
                    let i = url.lastIndexOf("/");
                    directory_box.innerHTML = decodeURIComponent(url.slice(i + 1));

                    if (directory_box.innerHTML.length == 0) {
                        directory_box.innerHTML = share_name;
                    }

                }         

                function queue_song(filename) {   
                    file_list.push(filename);
            
                    let span = document.createElement('span');
            
                    track_list.appendChild(span);
            
                    span.innerHTML = "<div><a href=\"javascript:void(0)\" onclick=\"loadSong('" + filename + "')\">" + filename + "</a></div>";

                    if (file_list.length == 1) {
                        play();
                    }
            
                }   

                function play_pause() {
                    if (audio_player.paused) {
                        play();
                    } else {
                        pause();
                    }
                }

                function play() {
                    play_pause_button.innerHTML = "Pause";
                    audio_player.play();
                }

                function pause() {
                    play_pause_button.innerHTML = "Play";
                    audio_player.pause();
                }
            
                function next() {            
                    current_index++;
                    if (current_index > file_list.length-1) current_index = 0;
                    audio_player.src = file_list[current_index];
                }
            
                function previous() {
                    current_index--;
                    if (current_index < 0) current_index = file_list.length-1;
                    audio_player.src = file_list[current_index];
                }
            
                play_pause_button.addEventListener('click', () => { play_pause(); });
            
                next_button.addEventListener('click', () => { next(); });
                previous_button.addEventListener('click', () => { previous(); }); 
                    
                // skip to next file when current one ends
                audio_player.addEventListener("ended", () => { next(); });
            
                // load a file and attempt to find it in the list for the sake of highlighting + next/previous track stuff
                function loadSong(filename) {    
                    audio_player.src = filename;
                                
                    progress_bar.style.width = '0%';
                    play();
            
                    current_index = file_list.findIndex(function(f) {
                        return decodeURIComponent(filename).endsWith(decodeURIComponent(f)) || filename == decodeURIComponent(f);
                    });
                }
            </script>
            </html>
            """;

    }
}
