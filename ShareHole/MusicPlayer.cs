using ImageMagick;

namespace ShareHole {
    public static class MusicInfo {
        internal static ConcurrentCache<MusicCacheItem> cache = new ConcurrentCache<MusicCacheItem>();

        internal static Dictionary<string, List<FileInfo>> folder_reference = new Dictionary<string, List<FileInfo>>();

        public class MusicCacheItem {
            internal string filename;
            string mime;

            public string title;
            public string artist;
            public string album;

            public int year;            

            public int track_number;

            public byte[] cover;
        }
    }


    public static class MusicPlayerData {
        public static string stylesheet = """
            :root {
                --border-thickness: 2px;
                --total-borders: 4;

                /* top - info */
                --top-info-area-height: 0px;
                --progress-bar-height: 12px;

                /* middle - files */
                --directory-box-height: calc(30px + var(--border-thickness));

                /* bottom - controls */
                --control-area-height: 100px;

                /* computed */
                --progress-bar-full-height: calc(var(--progress-bar-height));

                --bars: calc(var(--border-thickness) * var(--total-borders));

                --top-height: calc(var(--top-info-area-height));
                --bottom-height: calc(var(--control-area-height) + var(--border-thickness) + var(--progress-bar-height) );
            
                --middle-list-height: calc(100vh - var(--top-height) - var(--directory-box-height) - var(--bottom-height) - var(--bars));
                --middle-height: calc(100vh - var(--top-height) - var(--bottom-height) - var(--bars));
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
            
                border: solid var(--border-thickness) var(--main-color);
            
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
                width: 100%;
                height: fit-content;
            color: var(--text-color);
            }
            
            #music-info-artist {
                width: 100%;
                height: fit-content;
            color: var(--text-color);
            }
            
            #music-info-album {
                width: 100%;
                height: fit-content;
            color: var(--text-color);
            }
            
            #music-info-cover {            
                max-width: 100px;
                max-height: 100px;
                width: 100px;
                height: 100px;
            }            
                     
                    
            /* MIDDLE */
            
            #middle {    
                overflow:hidden;
                width: 100%;    
                display: block;
                                                
                height: var(--middle-height);
            
                border-bottom: solid var(--border-thickness) var(--main-color);
                border-left: solid var(--border-thickness) var(--main-color);
                border-right: solid var(--border-thickness) var(--main-color);
            }
            
            #directory-box {
                background-color:#101010; 
                z-index: 9999; 
                top: 0;
                left: 0;
                width: 100%;
                height: 30px; 
                text-align: center;                              
                        
                border-bottom: solid var(--border-thickness) var(--main-color);            
                overflow:hidden;
            }
                    
            #file-list-frame {
                width:100%;
                height:var(--middle-list-height); 
            }


            /* BOTTOM */

            #bottom {    
                width: 100%;   
                height: var(--bottom-height);

                overflow:hidden;
                            
                border: solid var(--border-thickness) var(--main-color);
                border-top: none;

                background-color: var(--background-color);
            }

            /* PROGRESS BAR */
            
            .progress-container {
                width: 100%;
                height: var(--progress-bar-height);
                background-color: var(--background-color);
                cursor: pointer;
            }
            
            .progress-bar {
                height: 100%;
                background-color: var(--main-color);
                width: 0%;
            }                   

            /* MAIN AUDIO CONTROLS */

            .audio-controls-container {
                text-align: center;
                display: flex;
                flex-direction: row;
                overflow:hidden;
                height: 100px;
                width: 100%;
            
                border-top: solid var(--border-thickness) var(--main-color);
            }
            
            #audio-info {
                display: flex;
                justify-content: left;
                flex-direction: column;
                height: 100vh;
                width: 100%;
                color: var(--text-color);
            }
            .audio-controls {
                display: flex;
                justify-content: right;
                height: 100vh;
                width: 25%;
                color: var(--text-color);
            }
            
            .audio-controls button {
                all: unset; 

                background-color: var(--background-color);
                color: var(--main-color);
            
                border: none;
                padding 0px;
                            
                font-family: 'Segoe UI Symbol', Tahoma, Geneva, Verdana, sans-serif !important;     
                font-size: calc(var(--bottom-height) * 0.5);
                text-align:center;
                align-content: center;
            
                height: calc(var(--control-area-height));
                width:  calc(var(--control-area-height));
            }

            .audio-controls button:hover {
                color: var(--background-color);
                background-color: var(--main-color);
            }         

            .button-separator {
                background-color: var(--main-color);
                height: 100%;
                width: var(--border-thickness);
                margin:0;
                border:none;
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
                    <source src="" type="audio/mp3">
                    Your browser does not support the audio element.
                </audio>
            
                <div id="top">
                    <div id="music-info-container">
                        <div id="music-info-cover"></div>
                        <div id="music-info-details">
                        </div>
                    </div>
                </div>

                <div id="middle">
                    <div id="directory-box">{current_directory_cleaned}</div>
                    <iframe id="file-list-frame" name="file_list_frame" src="{music_player_list_dir}"></iframe> 
                </div>

                <div id="bottom">            
                    <div class="progress-container" id="progress-container"> 
                        <div class="progress-bar" id="progress-bar"></div>
                    </div>        
                    <div class="audio-controls-container">
                        <div id="audio-info">
                        </div>
                        <div class="audio-controls">
                           <!-- <button id="previous-button">Previous</button> -->
                           <hr class="button-separator"/>
                           <button id="play-pause-button">♡</button>
                           <hr class="button-separator"/>
                           <!-- <button id="next-button">Next</button> -->
                        </div>
                    </div>
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
                const info_box = document.getElementById('audio-info'); 
                        
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
                    //info_box.innerHTML = fetch();
                    
                }

                function change_directory(url) {
                    list_frame.src = url;
                    change_directory_visual(url);
                }         

                function change_directory_visual(url) {
                    let i = decodeURIComponent(url).lastIndexOf("/");
                    directory_box.innerHTML = decodeURIComponent(url).slice(i + 1);
            
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
                    play_pause_button.innerHTML = "♥";
                    audio_player.play();
                }

                function pause() {
                    play_pause_button.innerHTML = "♡";
                    audio_player.pause();
                }
            
                function next() {            

                }
            
                function previous() {

                }
            
                play_pause_button.addEventListener('click', () => { play_pause(); });
            
                //next_button.addEventListener('click', () => { next(); });
                //previous_button.addEventListener('click', () => { previous(); }); 
                    
                // skip to next file when current one ends
                audio_player.addEventListener("ended", () => { next(); });
                
                // load a file and attempt to find it in the list for the sake of highlighting + next/previous track stuff
                function load_song_and_folder(filename) {    
                    audio_player.src = filename;
                    
                    update_title();
                                
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
