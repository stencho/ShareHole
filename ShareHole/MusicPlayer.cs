namespace ShareHole {
    public static class MusicPlayerData {
        public static string stylesheet = """
            :root {
                --info-buttons-height: 100px;
                --progress-bar-height: 12px;
            
                --border-thickness: 2px;
                --total-border-thickness: calc(var(--border-thickness) * 3);

                --progress-bar-full-height: calc(var(--progress-bar-height) + var(--border-thickness));
                --top-height: calc(var(--info-buttons-height) + var(--progress-bar-height) + var(--border-thickness));
                --bottom-height: calc(100vh - var(--top-height) - var(--total-border-thickness));
            }
            
            body {
                display: flex;
                overflow: hidden;
                flex-wrap: wrap;            
            
                width: 100vw;            
                height: 100vh;
                        
                max-width: 100vw;
            
                margin: 0;
            }
            
            iframe { border: none; }                          
            audio-player { display: none; }
                    
            
            /* TOP */
            
            #top {    
                width: 100%;   
                height: var(--top-height);
                display: flex;
                flex-wrap: wrap;
            
                border: solid 2px var(--main-color);
            
                background-color: var(--background-color);
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
                display: flex;
                justify-content: end;
                height: 100px;
                width: 100%;
                color: var(--background-color);
            }
            
            .audio-controls button {
                background-color: var(--background-color);
                color: var(--main-color);
            
                border: none;
                border-left: solid 2px var(--main-color);
                            
                font-family: 'Segoe UI Symbol', Tahoma, Geneva, Verdana, sans-serif !important;     
                font-size: calc(var(--top-height) * 0.5);
                text-align:center;
                align-content: center;

                height: var(--info-buttons-height);
                width: var(--info-buttons-height);
            }
            
            .audio-controls button:hover {
                color: var(--background-color);
                background-color: var(--main-color);
            }           
                        
            /* PROGRESS BAR */
            
            .progress-container {
                width: 100%;
                height: var(--progress-bar-height);
                background-color: var(--background-color);
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
                display: block;
                                                
                height: var(--bottom-height);
            
                border-bottom: solid 2px var(--main-color);
                border-left: solid 2px var(--main-color);
                border-right: solid 2px var(--main-color);
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
                overflow:hidden;
            }
                    
            #file-list-frame {
                width:100%;
                height:calc(100% - 32px); /* 30px for the directory_box + 2 for the two 2px border */
            }
            """;

        public static string music_player_main_view = """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title></title>
                <link rel="stylesheet" href="base.css">
                <style>
                {stylesheet}
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
                    <div class="audio-controls-container">
                        <div class="audio-controls">
                           <button id="play-pause-button"></button>
                           <!-- <button id="previous-button">Previous</button> -->
                           <!-- <button id="next-button">Next</button> -->
                        </div>
                    </div>
                    <div class="progress-container" id="progress-container"> 
                        <div class="progress-bar" id="progress-bar"></div>
                    </div>
                </div>

                <div id="bottom">
                    <div id="directory-box">{current_directory_cleaned}</div>
                    <iframe id="file-list-frame" name="file_list_frame" src="{music_player_list_dir}"></iframe> 
                </div>
            </body>

            <script>
                const share_name = '{share_name}';            

                const list_frame = document.getElementById('file-list-frame');  
                const list_frame_window = list_frame.contentWindow;
                const list_frame_doc = list_frame.contentDocument;
            
                const audio_player = document.getElementById('audio-player');
            
                const play_pause_button = document.getElementById('play-pause-button');
                const next_button = document.getElementById('next-button');
                const previous_button = document.getElementById('previous-button');
            
                const progress_bar = document.getElementById('progress-bar');
                const progress_container = document.getElementById('progress-container');
            
                const directory_box = document.getElementById('directory-box'); 
                        
                let current_index = 0; 
                const file_list = [];
                
                // set the text in the current directory box above the file list
                directory_box.addEventListener('load', () => {
                    directory_box.innerHTML = '{current_directory}';                    
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

                function play_pause() {
                    if (audio_player.paused) {
                        play();
                    } else {
                        pause();
                    }
                }

                function play() {
                    play_pause_button.innerHTML = "";
                    audio_player.play();
                }

                function pause() {
                    play_pause_button.innerHTML = "";
                    audio_player.pause();
                }
            
                function next() {            

                }
            
                function previous() {

                }
            
                play_pause_button.addEventListener('click', () => { play_pause(); });
            
                next_button.addEventListener('click', () => { next(); });
                previous_button.addEventListener('click', () => { previous(); }); 
                    
                // skip to next file when current one ends
                audio_player.addEventListener("ended", () => { next(); });
                
                // load a file and attempt to find it in the list for the sake of highlighting + next/previous track stuff
                function load_song_and_folder(filename) {    
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
