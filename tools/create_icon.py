"""Build the original pixel icon from hand-plotted points (Pillow only)."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
im = Image.new("RGBA", (32, 32))
d = ImageDraw.Draw(im)
shape = [(3,29),(2,27),(3,24),(5,21),(6,17),(7,13),(9,11),(11,12),(13,15),(17,15),(20,11),(22,10),(24,11),(25,14),(25,18),(27,21),(29,25),(29,28),(27,30),(22,31),(18,30),(13,31),(8,30),(5,31)]
d.polygon(shape, fill="#caa0eb", outline="#654581")
d.line([(11,20),(11,21)], fill="#483454")
d.line([(21,20),(21,21)], fill="#483454")
d.line([(14,23),(15,24),(17,24),(18,23)], fill="#634072")
d.line([(8,23),(9,23)], fill="#e7acd5")
d.line([(23,23),(24,23)], fill="#e7acd5")
out = root / "src/Ditto.Desktop/Assets/ditto.ico"
out.parent.mkdir(parents=True, exist_ok=True)
im.resize((256,256), Image.Resampling.NEAREST).save(out, sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])
print(out)
