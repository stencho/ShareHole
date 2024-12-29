using System.Text;

namespace ChoonGang {
    public static class PlayerRenderer {
        public static string DrawPlayer() {
            return Program.base_html_strings_replaced("""
                
                <audio id="audio-player" preload="auto">
                    <source src="" type="audio/mp3">
                    Your browser does not support the audio element.
                </audio>                

                <div class="top-bar">
                    <!-- <img src="thumbnail.jpg" alt="Thumbnail" class="thumbnail"> -->
                    <div class="track-info">
                        <p class="track-info-item" id="title"></p>
                        <p class="track-info-item" id="artist"></p>
                        <p class="track-info-item" id="album"></p>
                    </div>
                </div>

                <div class="search-bar">
                    <input type="text" placeholder="Search..." oninput="searchMusic()">
                </div>

                <div class="music-list-container">
                    <iframe id="music-list-iframe" src="/list" frameborder="0"></iframe>
                </div>

                <div id="bottom">            
                    <div class="progress-container" id="progress-container"> 
                        <div class="progress-bar" id="progress-bar"></div>
                    </div>        
                    <div class="audio-controls-container">
                        <div class="audio-controls">
                            <hr class="button-separator"/>
                            <button id="previous-button">⏮</button>
                            <hr class="button-separator"/>
                            <button id="play-pause-button"></button>
                            <hr class="button-separator"/>
                            <button id="next-button">⏭</button>
                            <hr class="button-separator"/>
                        </div>
                    </div>
                </div>
                """,

                "Choon Gang",

                //scripts
                """   
                const audio_player = document.getElementById('audio-player');
                
                const play_pause_button = document.getElementById('play-pause-button');

                const next_button = document.getElementById('next-button');
                const previous_button = document.getElementById('previous-button');
            
                const progress_bar = document.getElementById('progress-bar');
                const progress_container = document.getElementById('progress-container');

                const title = document.getElementById('title');
                const artist = document.getElementById('artist');
                const album = document.getElementById('album');

                let current_file = "";
                
                let previous_track = "";
                let next_track = "";

                function searchMusic() {
                    const query = document.getElementById("searchInput").value.trim();
                    let sqlQuery = '';

                    if (query.startsWith('$')) {
                        const [column, condition] = query.substring(1).split(' ');
                        if (column && condition) {
                            sqlQuery = `SELECT * FROM music WHERE ${column} LIKE '%${condition}%'`;
                        }
                    } else {
                        sqlQuery = `SELECT * FROM music WHERE title LIKE '%${query}%' OR artist LIKE '%${query}%' OR album LIKE '%${query}%' OR path LIKE '%${query}%'`;
                    }

                    console.log('SQL Query:', sqlQuery);
                    // Send the query to the server or process it as needed
                    // For now, we will just log it
                }

                
                function play_pause() {
                    if (audio_player.paused) {
                        play();
                    } else {
                        pause();
                    }
                }
                
                function update_previous_and_next_track(filename) {                
                    fetch('/prev/' + encodeURIComponent(filename))
                        .then(response => response.text())
                        .then(data => {
                            console.log('prev: ' + data);
                            previous_track = data;
                        });
                
                    fetch('/next/' + encodeURIComponent(filename))
                        .then(response => response.text())
                        .then(data => {
                            console.log('next: ' + data);
                            next_track = data;
                        });                 
                }

                function update_tags(filename) {                    
                    fetch('/info/' + encodeURIComponent(filename))
                        .then(response => response.text())
                        .then(data => {
                            console.log('info: ' + data);
                            let j = JSON.parse(data);

                            title.innerHTML = j.title;

                            artist.innerHTML = j.artist;

                            let year = "";
                            if (j.year > 0) year = "(" + j.year + ")";

                            let album_s = "";
                            if (j.album != "") album_s = j.album;

                            let final_str = album_s;
                            if (final_str != "" && year != "0") final_str = final_str + " ";
                            if (year != "0") final_str = final_str + year.toString();

                            album.innerHTML = final_str;
                        });
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
                    play_song(encodeURIComponent(next_track));
                }
                
                function previous() {
                    play_song(encodeURIComponent(previous_track));
                }

                function play_song(filename) {    
                    audio_player.src = '/get/' + filename;
                    play();         
                    current_file = filename;
                    progress_bar.style.width = '0%';
                    update_tags(filename);
                    update_previous_and_next_track(filename);                   
                }                

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
            
                play_pause_button.addEventListener('click', () => { play_pause(); });                
                next_button.addEventListener('click', () => { next(); });
                previous_button.addEventListener('click', () => { previous(); }); 
                        
                // skip to next file when current one ends
                audio_player.addEventListener("ended", () => { next(); });
                    
                """,

                //styles
                """
                :root {                
                    --border-thickness: 2px;
                    --border-count: 4;
                
                    --top-info-area-height: 100px;
                    --search-box-height: calc(40px + var(--border-thickness));
                
                    --vertical-border-total: calc(var(--border-thickness) * var(--border-count));
                    --bottom-height: 0px;
                    --middle-height: calc(100vh - var( --top-info-area-height) - var(--search-box-height) - var(--bottom-height) - var(--vertical-border-total));
                    
                    --progress-bar-height: 12px;
                    --control-area-height: 100px;
                    --bottom-height: calc((var(--control-area-height) + var(--progress-bar-height)) + var(--border-thickness));
                }

                body {
                }

                .track-info-item {
                    margin: 0;
                    padding: 0;
                }
                
                #title{
                    margin: 2px;
                    padding: 0;

                    color: var(--main-color);
                
                    font-size: 18pt;
                    text-align:center;
                }
                #artist {
                    font-size: 16pt;
                    color: var(--secondary-color);
                }
                #album {
                    
                }

                .top-bar {
                    display: flex;
                    align-items: center;
                    height: 100px;                    
                    color: white;
                    padding: 0px;
                    background-color: var(--background-color);
                    border: var(--border-thickness) solid var(--main-color);
                }

                .thumbnail {
                    width: 100px;
                    height: 100px;
                    margin-right: 10px;
                }
                
                .track-info {
                    display: flex;
                    flex-direction: column;
                    width: 100%;
                    text-align:center;
                }

                .search-bar {
                    height: var(--search-box-height);
                    padding: 0px;
                    background-color: var(--background-color);
                    border: var(--border-thickness) solid var(--main-color);
                    border-top: none;
                    text-align: center;
                    align-content: center;
                }

                .search-bar input {
                    width: 98%;
                    padding: 2px;
                    font-size: 16px;
                    color: var(--text-color);
                    background-color: var(--background-color);
                    border:none;
                    outline:none;
                }
                .search-bar input:focus {
                    outline:none;
                    border:none;
                }

                .music-list-container {
                    width: 100%;
                    height: var(--middle-height);
                    padding: 0px;
                    text-align: center;
                }

                #music-list-iframe {
                    width: 100%;
                    height: 100%;
                    border: none;
                }

                
                /* BOTTOM */
                
                #bottom {    
                    width: calc(100vw - 4px);
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
                    border-top: solid var(--border-thickness) var(--main-color);
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
                    flex-direction: column;
                    overflow:hidden;
                    height: 100px;
                    width: 100%;
                
                    border-top: solid var(--border-thickness) var(--main-color);
                    border-right: solid var(--border-thickness) var(--main-color);
                }
                
                .audio-controls {
                    display: flex;
                    justify-content: center;
                    height: 100vh;
                    width: 100%;
                    color: var(--background-color);
                }
                
                .audio-controls button {
                    all: unset; 
                
                    background-color: var(--background-color);
                    color: var(--main-color);
                
                    border: none;
                    padding 0px;
                                
                    font-family: 'Segoe UI Symbol', Tahoma, Geneva, Verdana, sans-serif !important;     
                    font-size: calc(var(--top-height) * 0.5);
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
                """);
          
        }

    }
}