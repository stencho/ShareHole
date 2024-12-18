using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareHole.Threads {

    class MusicDB {

    }

    public static class MusicPlayer {
        // MusicDB db;

        public static string music_player_main_view = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title></title>
                <link rel="stylesheet" href="base.css">
                <style>
                    body {
                        margin: 0;
                        display: flex;
                        height: 100vh;
                    }
                    #list_frame, #player_frame {
                        width: 50%;
                        height: 100%;
                    }
                    iframe {
                        width: 100%;
                        height: 100%;
                        border: none;
                    }
                </style>
            </head>
            <body>
                <iframe id="list_frame" name="list_frame" src="{list}"></iframe>
                <iframe id="player_frame" name="player_frame" src="{music_player_url}"></iframe>
            </body>
            <script>
                function update_title() {

                }

                function queue_song(filename) {     
                    const player_frame = document.getElementById('player_frame').contentWindow;
                    player_frame.queue(filename, 'test');
                }

                function change_directory(url) {
                    const f = document.getElementById('list_frame');
                    f.src = url;
                }
            </script>
            </html>
            """;

        public static string music_player_content = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title></title>
                <link rel="stylesheet" href="base.css">
                <style>
                    body {
                        font-family: Arial, sans-serif;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100%;
                        margin: 0;
                        overflow:hidden;
                        background-color: #f4f4f4;
                    }

                    .audio-player-container {
                        height: 100%;
                        width: 100%;
                        position: 0 0;
                        background-color: #fff;
                        padding: 20px;
                        border-radius: 8px;
                        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
                        text-align: center;
                        display: flex;
                        flex-direction: column;
                        overflow:hidden;
                    }

                    .audio-title {
                        font-size: 18px;
                        font-weight: bold;
                        margin-bottom: 15px;
                    }

                    .audio-controls {
                        display: flex;
                        justify-content: space-around;
                        margin-bottom: 10px;
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

                    .progress-container {
                        width: 100%;
                        height: 10px;
                        background-color: #e0e0e0;
                        border-radius: 5px;
                        margin-top: 10px;
                        cursor: pointer;
                    }

                    .progress-bar {
                        height: 100%;
                        background-color: #007bff;
                        width: 0%;
                        border-radius: 5px;
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
                <div class="audio-player-container">
                    <div class="audio-title" id="current_file"></div> 

                    <div class="audio-controls">
                        <button id="play-button">Play</button>
                        <button id="pause-button">Pause</button>
                        <button id="previous-button">Previous</button>
                        <button id="next-button">Next</button>
                    </div>

                    <audio id="audio-player" preload="auto">
                        <source src="" type="audio/mp3">
                        Your browser does not support the audio element.
                    </audio>

                    <div class="progress-container" id="progress-container">
                        <div class="progress-bar" id="progress-bar"></div>
                    </div>

                    <ul class="track-list">
                        {track_list}
                    </ul>
                </div>

                <script>
                    const audio_player = document.getElementById('audio-player');

                    const play_button = document.getElementById('play-button');
                    const pause_button = document.getElementById('pause-button');
                    const next_button = document.getElementById('next-button');
                    const previous_button = document.getElementById('previous-button');

                    const progress_bar = document.getElementById('progress-bar');
                    const progress_container = document.getElementById('progress-container');

                    const current_file_title = document.getElementById('current-file'); 

                    let current_index = 0; 

                    const localdir = '{local_dir}';
                    const file_list = {file_array};
            
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

                    document.getElementById('next_button').addEventListener('click', () => { next(); });
                    document.getElementById('previous_button').addEventListener('click', () => { previous(); }); 
                    
                    // skip to next file when current one ends
                    audio_player.addEventListener("ended", () => { next(); });

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
            </body>
            </html>       
            """;
    }
}
