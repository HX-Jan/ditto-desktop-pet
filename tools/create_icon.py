"""Create the executable/tray icon from the app's original exported pixel drawing."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = root / "artifacts/frames-v020/Idle-00.png"
if not source.exists():
    raise SystemExit("Export app frames using --export-demo --output artifacts/frames-v020 first.")
im = Image.open(source).convert("RGBA")
out = root / "src/Ditto.Desktop/Assets/ditto.ico"
im.resize((256, 256), Image.Resampling.NEAREST).save(
    out, sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])
print(out)
