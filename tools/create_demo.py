"""Render a labeled showcase from the app's actual exported v0.2 frames (Pillow).
This is an animation showcase, not a desktop recording.
"""
from pathlib import Path
import math
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
assets = root / "artifacts/frames-v020"
font_path = Path("C:/Windows/Fonts/msyh.ttc")
font = ImageFont.truetype(str(font_path), 23)
small = ImageFont.truetype(str(font_path), 16)
title = ImageFont.truetype(str(font_path), 34)
stages = [
    ("Idle", "今天，也想陪着你", "圆润的像素轮廓 · 眨眼与呼吸", 32, None),
    ("Walking", "慢慢跟着你", "底部陪伴 · 开启玩耍后自由追逐", 24, "Question"),
    ("Petting", "再摸摸嘛", "不按键，在头顶轻轻来回移动", 24, "Heart"),
    ("Reacting", "噗叽，软乎乎", "点击回弹 · 连续戳会小小委屈", 7, None),
    ("Jumping", "看我看我！", "小跳、伸懒腰和偶尔撒娇", 16, "Heart"),
    ("Stretching", "伸一个大懒腰", "活泼／安静，随时切换", 16, None),
    ("Walking", "把球抛给我", "拖动抛球 · 追球 · 顶球", 32, "Heart"),
    ("Yawning", "有一点点困了", "无音效 · 用动作和表情说话", 16, None),
    ("Sleeping", "晚安 Zzz", "全屏时自动隐藏 · 不抢键盘焦点", 24, None),
]
frames = []
for index, (state, heading, subtitle, count, icon) in enumerate(stages):
    for i in range(count):
        canvas = Image.new("RGB", (900, 480), "#171321")
        d = ImageDraw.Draw(canvas)
        d.rounded_rectangle((24, 24, 876, 456), radius=24, fill="#251e33", outline="#4b385e")
        d.text((56, 49), "百变怪桌宠", font=title, fill="#f2e6fb")
        d.rounded_rectangle((710, 58, 845, 93), radius=12, fill="#463153")
        d.text((730, 64), "v0.2.0 预览", font=small, fill="#edcaff")
        d.text((58, 104), "一小团紫色，今天更想和你玩。", font=small, fill="#b3a0c3")
        d.line((58, 367, 842, 367), fill="#735589", width=2)
        d.text((58, 393), heading, font=font, fill="#f0d7ff")
        d.text((430, 400), subtitle, font=small, fill="#b9a7c8")
        sprite = Image.open(assets / f"{state}-{i % 32:02}.png").convert("RGBA")
        sprite = sprite.resize((192, 192), Image.Resampling.NEAREST)
        x = 342 + (int(20 * math.sin(i / 8)) if state == "Walking" else 0)
        y = 175
        canvas.paste(sprite, (x, y), sprite)
        if icon:
            emote = Image.open(assets / f"Emote-{icon}.png").convert("RGBA").resize((64, 64), Image.Resampling.NEAREST)
            canvas.paste(emote, (x + 151, y - 13), emote)
        if index == 6:
            ball = Image.open(assets / "Ball.png").convert("RGBA").resize((40, 40), Image.Resampling.NEAREST)
            bx = 590 + int(70 * math.sin(i / 8))
            by = 327 - int(70 * abs(math.sin(i / 7)))
            canvas.paste(ball, (bx, by), ball)
        for n in range(len(stages)):
            d.ellipse((60 + n*15, 440, 65 + n*15, 445), fill="#d7b1f4" if n == index else "#5a426b")
        frames.append(canvas)
frames[0].save(root/"docs/demo.gif", save_all=True, append_images=frames[1:], duration=100, loop=0, optimize=True)
frames[0].save(root/"docs/preview.png")
print(root/"docs/demo.gif")
