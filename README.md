## ShareHole
An HTTP file server for sharing directories with low (but not zero) security, and a focus on security through extreme obscurity and minimization of features. It does not support POST requests, it's incapable of sharing files outside of user-defined shares, and it can't modify anything on disk except the server config file. Very asynchronous.

### Requirements
FFMpeg, ImageMagick, and Ghostscript

### Configuration and usage
Use the -c command line argument to set the current config directory.

ShareHole uses a "passdir", a directory which goes at the start of the URL, to let the server know you're cool. The default is "loot", so you would access a share called "share" though "example.com:8080/loot/share/".

There are also several "command directories" which go after the passdir, i.e. "/loot/to_png/share/image.jpg" would return PNG data, regardless of image format
- /thumbnail/ will produce a thumbnail

- /to_jpg/ will convert an image to JPG, with compression settings defined in the server config's \[conversion\] section
- /to_png/ will convert an image to lossless PNG 

This means that the server is able to use ImageMagick to render RAWs and Adobe files for you.

- /transcode/ will transcode a video to browser-friendly MP4 and send the result to the client. This uses FFMPeg, so it works on most video formats.

The config loader will write the default server config below, including comments, to config_dir/server on first start, and the server will do its best to keep this config file structure.

'server' config file
```ini
# General server settings
[server]
# Specify which adapter and port to bind to
prefix=localhost
port=8080
# The name of the first section of the URL, required to access shares
# For example: example.com:8080/loot/share
passdir=loot
# The number of threads for handling requests and uploads 
# This includes thumbnails, so if you're using gallery mode, you may want to increase this
threads=100
# The size of each partial transfer chunk's buffer size in kilobytes
transfer_buffer_size=512
# Look for base.html and base.css in the config directory instead of loading them from memory
use_html_file=false
use_css_file=false
# 0 = Logging off, 1 = high importance only, 2 = all messages
log_level=1

# UI color settings in R,G,B,A format
[theme]
main_color=242,191,241,255
main_color_dark=203,115,200,255
secondary_color=163,212,239,255
secondary_color_dark=110,180,210,255
text_color=235,235,235,255
background_color=16,16,16,255
secondary_background_color=69,28,69,255

# Settings for converting between different file types
[conversion]
# Toggle between lossless and compressed JPEG when using /to_jpg
jpeg_compression=true
# Quality level, from 0-100
jpeg_quality=85

# Settings for transcoding video files to MP4 and streaming them over the network
[transcode]
# Switch between using a variable or fixed bit rate to determine video quality and size
# It is recommended that you use a variable bit rate
use_variable_bit_rate=true
# Variable bit rate quality, lower values improve quality but increase file size
# Values around 18-25 are recommended
vbr_quality_factor=22
# The bit rate of the MP4 transcoding process, in Kb
cbr_bit_rate_kb=1000
# Determines how many threads are started for each /transcode/
threads_per_video_conversion=4

# Settings for the default "list" share style
[list]
# Display a play button next to video files, which when clicked will transcode the video
# to x264 MP4 and stream that to the client, from start to finish
# Seeking while the file is loading is possible in FireFox, but not Chrome
show_stream_button=true
# Display "PNG" and "JPG" buttons next to certain files which normally wouldn't be renderable in browser
show_convert_image_buttons=true
# Will modify URLs in the list to point to, for example, /to_jpg/ when the file is a .dng RAW
# the others do the same thing but for video/audio
convert_images_automatically=false
convert_videos_automatically=false
convert_audio_automatically=false

# Settings for the 'gallery' view style
[gallery]
# Thumbnail maximum resolution for both x and y axes
thumbnail_size=192
# true = JPEG thumbnails, false = PNG thumbnails, prettier, but uses more data
thumbnail_compression=false
# JPEG compression quality; 0-100
thumbnail_compression_quality=60
# Does the same thing as the options in [list], but for the gallery
# On by default
convert_images_automatically=true
convert_videos_automatically=true
convert_audio_automatically=true
```

The shares file is more free form, but every \[section\] must contain a "path" key, and all keys must be of the structure "key=value"

'shares' config file
```ini
# A basic share
# example.com:8080/loot/a share/
[a share]
path=D:\\shared
show_directories=true

# A picture/video gallery share
# example.com:8080/loot/pics/
[pics]
path=D:\\pictures
show_directories=true
style=gallery

# A music library share
# example.com:8080/loot/music/
[music]
path=D:\\music
show_directories=true
extensions=ogg mp3 wav flac alac ape m4a wma jpg jpeg bmp png gif 
```

- path: required, the folder you want to be visible at this URL
- show_directories: enables or disables viewing sub-directories
- extensions: limit the listed files to these extensions
- group_by: 'extension' or 'type'
- style: only list or gallery for now. list is default. 