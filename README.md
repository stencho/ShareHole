## ShareHole
An HTTP file server for sharing directories with low (but not zero) security, and a focus on security through extreme obscurity and minimization of features. It does not support POST requests, it's incapable of sharing files outside of user-defined shares, and it can't modify anything on disk except the server config file. Very asynchronous.

### Configuration and usage
Use the -c command line argument to set the current config directory.

ShareHole uses a "passdir", a directory which goes at the start of the URL, to let the server know you're cool. The default is "loot", so you would access a share called "share" though "example.com:8080/loot/share/".

There are also several "command dirs", which go after the passdir:
- /thumbnail/ will produce a thumbnail
- /to_jpg/ will convert an image to JPG, with compression settings defined in the server config's \[conversion\] section
- /to_png/ will convert an image to lossless PNG

"/loot/to_png/share/image.jpg" would return PNG data, for example.

This means that the server is able to use ImageMagick to render RAWs and Adobe files for you.

The config loader will write the default server config below to config_dir/server on first start, and the server will do its best to keep this config file structure.

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

# Look for base.html and base.css in the config directory instead of loading them from memory
use_html_file=false
use_css_file=false

# 0 = Logging off, 1 = high importance only, 2 = all messages
log_level=1

# Settings for converting between different file types
[conversion]
# Toggle between lossless and compressed JPEG when using /to_jpg
jpeg_compression=true
# Quality level, from 0-100
jpeg_quality=85

# Settings for the 'gallery' view style
[gallery]
thumbnail_size=192
# JPEG compression quality; 0-100
thumbnail_compression_quality=35
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

### TODO
- Better thumbnails for videos; correct aspect ratio, icons to denote that the video has sound and that it is a video, maybe animated gif thumbnails on mouseover?
- Pop-over image/video viewer for the gallery view style