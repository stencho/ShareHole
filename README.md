## ZeroDir
An HTTP file server designed to share low security files

### Configuration and usage
Use the -c command line argument to set the current config directory.

'server' config file
```ini
# General server options
[server]
# Specify which adapter and port to bind to
prefix=localhost
port=8080

# The name of the first section of the URL, required to access shares
# For example: example.com:8080/loot/share
passdir=loot

# The amount of threads for handling requests
# These threads dispatch other threads and thus not many are needed
threads=4

# Look for base.html and base.css in the config directory
use_html_file=false
use_css_file=false

# Image/Video thumbnail gallery options
[gallery]
thumbnail_size=192
thumbnail_builder_threads=8
```

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
- Add the ability to seek/resume downloads