import sys, subprocess, imageio_ffmpeg
from PIL import Image
FF = imageio_ffmpeg.get_ffmpeg_exe()
src, out = sys.argv[1], sys.argv[2]
times = [float(x) for x in sys.argv[3].split(",")]
ims = []
for t in times:
    p = f"/tmp/_f.png"
    subprocess.run([FF, "-loglevel", "error", "-y", "-ss", str(t), "-i", src, "-frames:v", "1", "-vf", "scale=480:-1", p], check=True)
    ims.append(Image.open(p).copy())
W, H = ims[0].size
cols = min(4, len(ims))
rows = (len(ims) + cols - 1) // cols
s = Image.new("RGB", (W * cols, H * rows))
for i, im in enumerate(ims):
    s.paste(im, ((i % cols) * W, (i // cols) * H))
s.save(out)
