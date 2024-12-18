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

        public static string box_overlay = """            
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Movable and Resizable Div</title>
                <link rel="stylesheet" href="base.css">
                <style>
                    /* Main body style */
                    body {
                        margin: 0;
                        height: 100vh;
                        overflow: hidden;
                        font-family: Arial, sans-serif;
                    }

                    /* Movable and resizable box */
                    #box {
                        position: absolute;
                        top: 50px;
                        left: 50px;
                        width: 200px;
                        height: 150px;
                        background-color: lightblue;
                        border: 2px solid #0073e6;
                        overflow: auto;
                        box-shadow: 2px 2px 5px rgba(0, 0, 0, 0.2);
                        cursor: move; /* Indicate the box can be moved */
                        display:none;
                        overflow: hidden;
                    }
                    .resize-handle {
                        position: absolute;
                        bottom: 0;
                        right: 0;
                        width: 0;
                        height: 0;
                        border-left: 10px solid transparent;
                        border-top: 10px solid transparent;
                        border-right: 10px solid #0073e6; /* Blue triangle */
                        border-bottom: 10px solid #0073e6;
                        cursor: nwse-resize; /* Resize cursor */
                        opacity: 0.7; /* Slight transparency */
                    }
                    .resize-handle:hover {
                        opacity: 1; /* Fully opaque when hovered */
                    }

                    .movebar {
                        width: 100%;
                        height: 20px;
                        background-color: lightgray;
                    }

                    /* Box content */
                    #box p {
                        margin: 10px;
                    }
                </style>
            </head>
            <body>
                <!-- Resizable and Movable Div -->
                <div id="box">
                    <div class="movebar"></div>
                    <div>
                    <iframe src="{music_player_url}" id="music_player" width="100%" height="100%"></iframe>
                    </div>
                     <div class="resize-handle"></div>
                </div>

                <div style="width: 100vw; height: 100vh; overflow: auto; ">{list}</div>

                <script>
                    const box = document.getElementById('box');
                    const inner = document.getElementById('box_inner');
                    const resizeHandle = document.querySelector('.resize-handle');

                    let isDragging = false;
                    let isResizing = false;
                    let offsetX, offsetY, initialWidth, initialHeight;

                    // Get the viewport dimensions
                    const getViewportWidth = () => window.innerWidth;
                    const getViewportHeight = () => window.innerHeight;

                    function close_box(){
                        box.innerHTML = ''; 
                        box.style.display = 'none';
                    }

                    // Load saved position and size
                    window.onload = () => {
                        const savedPosition = localStorage.getItem('boxPosition');
                        const savedSize = localStorage.getItem('boxSize');

                        if (savedPosition) {
                            const { top, left } = JSON.parse(savedPosition);
                            box.style.top = top;
                            box.style.left = left;
                        }

                        if (savedSize) {
                            const { width, height } = JSON.parse(savedSize);
                            box.style.width = width;
                            box.style.height = height;
                        }
                        
                        enforceBoundaries();
                        box.style.display = 'block';
                    };

                    // Save position and size to localStorage
                    const saveState = () => {
                        const position = {
                            top: box.style.top,
                            left: box.style.left,
                        };
                        const size = {
                            width: box.style.width,
                            height: box.style.height,
                        };
                        localStorage.setItem('boxPosition', JSON.stringify(position));
                        localStorage.setItem('boxSize', JSON.stringify(size));
                    };

                    // Enforce boundaries for dragging
                    const enforceBoundaries = () => {
                        const rect = box.getBoundingClientRect();
                        const viewportWidth = getViewportWidth();
                        const viewportHeight = getViewportHeight();

                        // Ensure box stays within bounds
                        if (rect.left < 0) box.style.left = `0px`;
                        if (rect.top < 0) box.style.top = `0px`;
                        if (rect.right > viewportWidth) box.style.left = `${viewportWidth - rect.width}px`;
                        if (rect.bottom > viewportHeight) box.style.top = `${viewportHeight - rect.height}px`;
                    };

                    // Enforce boundaries for resizing
                    const enforceResizeBoundaries = () => {
                        const rect = box.getBoundingClientRect();
                        const viewportWidth = getViewportWidth();
                        const viewportHeight = getViewportHeight();

                        // Prevent resizing beyond viewport boundaries
                        if (rect.right > viewportWidth) box.style.width = `${viewportWidth - rect.left}px`;
                        if (rect.bottom > viewportHeight) box.style.height = `${viewportHeight - rect.top}px`;

                        // Prevent shrinking too small
                        if (parseInt(box.style.width) < 50) box.style.width = '50px';
                        if (parseInt(box.style.height) < 50) box.style.height = '50px';
                    };

                    // Dragging Logic
                    box.addEventListener('mousedown', (e) => {
                        if (e.target === resizeHandle) return; // Ignore dragging when resizing

                        isDragging = true;

                        // Calculate the initial mouse position relative to the box
                        offsetX = e.clientX - box.getBoundingClientRect().left;
                        offsetY = e.clientY - box.getBoundingClientRect().top;

                        e.preventDefault(); // Prevent text selection
                    });

                    document.addEventListener('mousemove', (e) => {
                        if (isDragging) {
                            const left = e.clientX - offsetX;
                            const top = e.clientY - offsetY;

                            box.style.left = `${left}px`;
                            box.style.top = `${top}px`;

                            enforceBoundaries(); // Prevent dragging offscreen
                        }

                        if (isResizing) {
                            const deltaX = e.clientX - initialWidth;
                            const deltaY = e.clientY - initialHeight;

                            box.style.width = `${deltaX}px`;
                            box.style.height = `${deltaY}px`;

                            enforceResizeBoundaries(); // Prevent resizing offscreen
                        }
                    });

                    document.addEventListener('mouseup', () => {
                        if (isDragging) {
                            isDragging = false;
                            saveState(); // Save new position
                        }

                        if (isResizing) {
                            isResizing = false;
                            saveState(); // Save new size
                        }
                    });

                    // Resizing Logic
                    resizeHandle.addEventListener('mousedown', (e) => {
                        isResizing = true;

                        // Capture initial mouse position and box size
                        initialWidth = e.clientX - box.offsetWidth;
                        initialHeight = e.clientY - box.offsetHeight;

                        e.preventDefault();
                    });

                    // Listen for window resize to reapply boundaries
                    window.addEventListener('resize', enforceBoundaries);
                    </script>
            </body>
            </html>
            """;

        public static string music_player_content = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Audio Player with Progress Bar and File List</title>
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

                    audio {
                        display: none; /* Make the audio element invisible */
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

                    .file-list {
                        margin-top: 15px;
                        padding: 0;
                        list-style-type: none;
                        text-align: left;
                        max-height: 100vh; /* Limit the height of the file list */
                        overflow-y: auto; /* Enable scrolling if the list exceeds 500px */
                    }

                    .file-list li {
                        margin: 5px 0;
                        cursor: pointer;
                        color: #007bff;
                    }

                    .file-list li:hover {
                        text-decoration: underline;
                    }
                </style>
            </head>
            <body>
                <div class="audio-player-container">
                    <div class="audio-title" id="currentFile">A.mp3</div> <!-- Current file title -->

                    <div class="audio-controls">
                        <button id="playBtn">Play</button>
                        <button id="pauseBtn">Pause</button>
                        <button id="prevBtn">Previous</button>
                        <button id="nextBtn">Next</button>
                    </div>

                    <audio id="audio" preload="auto">
                        <source src="A.mp3" type="audio/mp3">
                        Your browser does not support the audio element.
                    </audio>

                    <div class="progress-container" id="progressContainer">
                        <div class="progress-bar" id="progressBar"></div>
                    </div>

                    <ul class="file-list">
                        {track_list}
                    </ul>
                </div>

                <script>
                    const audio = document.getElementById('audio');
                    const playBtn = document.getElementById('playBtn');
                    const pauseBtn = document.getElementById('pauseBtn');
                    const progressBar = document.getElementById('progressBar');
                    const progressContainer = document.getElementById('progressContainer');
                    const fileList = {file_array};
                    const currentFileTitle = document.getElementById('currentFile'); // Current file title element
                    let currentIndex = 0;  // Keep track of the current song's index
                    const localdir = '{local_dir}';

                    // Play the audio when the play button is clicked
                    playBtn.addEventListener('click', () => {
                        audio.play();
                    });

                    // Pause the audio when the pause button is clicked
                    pauseBtn.addEventListener('click', () => {
                        audio.pause();
                    });

                    function next() {            
                        currentIndex++;
                        if (currentIndex > fileList.length-1) currentIndex = 0;
                        audio.src = fileList[currentIndex];
                        currentFileTitle.textContent = currentIndex + decodeURIComponent(fileList[currentIndex]); 
                        audio.play();
                    }

                    // Move to the next song in the list
                    document.getElementById('nextBtn').addEventListener('click', () => {
                        next();
                    });

                    // Move to the previous song in the list
                    document.getElementById('prevBtn').addEventListener('click', () => {
                        currentIndex--;
                        if (currentIndex < 0) currentIndex = fileList.length-1;
                        audio.src = fileList[currentIndex];
                        currentFileTitle.textContent = currentIndex + decodeURIComponent(fileList[currentIndex]); 
                        audio.play();
                        
                    });
                    
                    audio.addEventListener("ended", () => {
                        next();
                    });

                    // Update the progress bar as the audio plays
                    audio.addEventListener('timeupdate', () => {
                        const progress = (audio.currentTime / audio.duration) * 100;
                        progressBar.style.width = progress + '%';
                    });

                    // Allow the user to click on the progress bar to seek to a specific time
                    progressContainer.addEventListener('click', (e) => {
                        const offsetX = e.offsetX;
                        const width = progressContainer.offsetWidth;
                        const newTime = (offsetX / width) * audio.duration;
                        audio.currentTime = newTime;
                    });

                    // Load and play the selected song from the list
                    function loadSong(filename) {            
                        currentIndex = fileList.findIndex(function(f) {
                            return decodeURIComponent(filename).endsWith(decodeURIComponent(f)) || filename == decodeURIComponent(f);
                        });

                        audio.src = filename;
                        currentFileTitle.textContent = currentIndex + decodeURIComponent(filename);  // Update the current file title
                        audio.play();
                    }
                </script>
            </body>
            </html>       
            """;
    }
}
