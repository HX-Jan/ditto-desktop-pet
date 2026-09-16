"""Compose a documentation demo from PNG frames exported by the actual app.

Run DittoDesktopPet.exe --export-demo --output artifacts/frames first.
Requires Pillow. This is an animation showcase, not a desktop recording.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
font_path = Path("C:/Windows/Fonts/msyh.ttc")
font = ImageFont.truetype(str(font_path), 22)
small = ImageFont.truetype(str(font_path), 15)
title = ImageFont.truetype(str(font_path), 32)
stages = [("Idle", "发一会儿呆", "安静陪伴，偶尔眨眼", 32),
          ("Walking", "慢慢挪过来", "在任务栏上方自由闲逛", 16),
          ("Reacting", "戳一下，软乎乎", "点击触发压扁与回弹", 6),
          ("Dragging", "拎起来看看", "按住鼠标拖动", 12),
          ("Landing", "噗叽，落地", "松手后回到屏幕底部", 6),
          ("Sleeping", "休息一下 Zzz", "右键菜单可以唤醒", 24)]
frames = []
for stage, heading, subtitle, count in stages:
    for i in range(count):
        canvas = Image.new("RGB", (800, 420), "#171321")
        d = ImageDraw.Draw(canvas)
        d.rounded_rectangle((28,28,772,392), radius=22, fill="#241d31", outline="#463352", width=1)
        d.text((58,54), "百变怪桌宠", font=title, fill="#efe4fa")
        d.rounded_rectangle((626,62,736,92), radius=12, fill="#3a2a49")
        d.text((643,66), "v0.1.0 预览", font=small, fill="#d9b6f3")
        d.text((58,106), "一小团紫色，陪你待在桌面上。", font=small, fill="#a996ba")
        d.line((58,324,740,324), fill="#705583", width=2)
        d.text((58,345), heading, font=font, fill="#e3c8f5")
        d.text((435,350), subtitle, font=small, fill="#a996ba")
        sprite = Image.open(root / "artifacts/frames" / f"{stage}-{i % 32:02}.png").convert("RGBA")
        sprite = sprite.resize((160,160), Image.Resampling.NEAREST)
        x = 306 + (i*3-24 if stage == "Walking" else 0)
        y = 164 - (28 if stage == "Dragging" else 0)
        canvas.paste(sprite, (x,y), sprite)
        frames.append(canvas)
out = root / "docs/demo.gif"
frames[0].save(out, save_all=True, append_images=frames[1:], duration=125, loop=0, optimize=True)
frames[0].save(root / "docs/preview.png")
print(out)
