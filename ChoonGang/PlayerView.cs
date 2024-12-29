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
                        <p class="track-info-item" id="title">Title</p>
                        <p class="track-info-item" id="artist">Artist</p>
                        <p class="track-info-item" id="album">Album</p>
                    </div>
                </div>

                <div class="search-bar">
                    <input type="text" placeholder="Search..." oninput="searchMusic()">
                </div>

                <div class="music-list-container">
                    <iframe id="music-list-iframe" src="/list" frameborder="0"></iframe>
                </div>
                """,

                "Choon Gang",

                //scripts
                """   
                const audio_player = document.getElementById('audio-player');
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
                        }) 
                
                    fetch('/next/' + encodeURIComponent(filename))
                        .then(response => response.text())
                        .then(data => {
                            console.log('next: ' + data);
                            next_track = data;
                        })                   
                }

                function update_tags() {

                }
                
                function play() {
                    audio_player.play();
                }
                
                function pause() {
                    audio_player.pause();
                }

                function next() {
                    play_song(next_track);
                }
                
                function previous() {
                    play_song(previous_track);
                }

                function play_song(filename) {    
                    audio_player.src = '/get/' + filename;
                    play();         
                    current_file = filename;
                    update_previous_and_next_track(filename);       
                }
                """,

                //styles
                """
                :root {                
                    --border-thickness: 2px;
                    --border-count: 2;
                
                    --top-info-area-height: 100px;
                    --search-box-height: calc(40px + var(--border-thickness));
                
                    --vertical-border-total: calc(var(--border-thickness) * var(--border-count));
                    --bottom-height: 0px;
                    --middle-height: calc(100vh - var( --top-info-area-height) - var(--search-box-height) - var(--bottom-height) - var(--vertical-border-total));
                }

                body {
                    overflow:hidden;
                }

                .track-info-item {
                    margin: 0;
                    padding: 0;
                }
                
                #title{
                    margin: 2px;
                    padding: 0;

                    color: var(--main-color);
                
                    font-size: 20pt;
                }

                .top-bar {
                    display: flex;
                    align-items: center;
                    height: 100px;                    
                    color: white;
                    padding: 0px;
                    background-color: var(--background-color);
                    border-bottom: var(--border-thickness) solid var(--main-color);
                }

                .thumbnail {
                    width: 100px;
                    height: 100px;
                    margin-right: 10px;
                }
                
                .track-info {
                    display: flex;
                    flex-direction: column;
                }

                .search-bar {
                    height: var(--search-box-height);
                    padding: 0px;
                    background-color: var(--background-color);
                    border-bottom: var(--border-thickness) solid var(--main-color);
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
                """);
          
        }

    }
}