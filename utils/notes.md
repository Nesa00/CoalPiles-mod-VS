winget install Microsoft.DotNet.SDK.8
winget install Gyan.FFmpeg
py utils/video-convert.py -i demo.mp4 -o demo.webp

# for vintage story mod db
py utils/video-convert.py -i demo.mp4 -o demo.webp -t 3 

py utils/video-convert.py -i media/video/demo.mp4 -o media/video/demo.webp -t 3 
py utils/video-convert.py -i media/video/demo1.mp4 -o media/video/demo1.webp -t 3 
py utils/video-convert.py -i media/video/demo2.mp4 -o media/video/demo2.webp -t 3 
py utils/video-convert.py -i media/video/demo3.mp4 -o media/video/demo3.webp -t 3 
py utils/video-convert.py -i media/video/demo4.mp4 -o media/video/demo4.webp -t 3 