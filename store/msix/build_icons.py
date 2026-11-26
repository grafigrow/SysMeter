#!/usr/bin/env python3
"""PC-ready variant of the Store icon builder (Windows).

Regenerates the MSIX tile/splash art for ONE app from code (no source images
needed) and writes the files with the exact names AppxManifest.xml expects
into the Assets/ folder next to this script.

Usage (PowerShell, from the repo root):
    pip install pillow
    python store\\msix\\build_icons.py netspeedmeter     # or: sysmeter

Output: store\\msix\\Assets\\Square150x150Logo.png, Square44x44Logo.png,
Square71x71Logo.png, Square310x310Logo.png, Wide310x150Logo.png,
StoreLogo.png, SplashScreen.png

Font: Arial Bold (ships with Windows). If it is missing, set MSIX_FONT to
the full path of any bold TrueType font.
"""
from PIL import Image, ImageDraw, ImageFont
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = HERE  # tiles land in OUT/<app>/, then get copied to OUT/Assets/


def find_font():
    for cand in (os.environ.get("MSIX_FONT", ""),
                 r"C:\Windows\Fonts\arialbd.ttf",
                 r"C:\Windows\Fonts\Arialbd.ttf",
                 "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"):
        if cand and os.path.exists(cand):
            return cand
    raise SystemExit("No bold TrueType font found. Set MSIX_FONT to a .ttf path.")


SS = 2048  # supersample size


def draw_netspeedmeter(size=SS):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pad = size * 0.03
    r = size * 0.22
    bw = int(size * 0.045)
    d.rounded_rectangle([pad, pad, size - pad, size - pad], radius=r,
                        fill=(0x18, 0x1C, 0x24, 255), outline=(0x38, 0xBD, 0xF8, 255), width=bw)
    inner = size - 2 * pad
    ax_w = inner * 0.30
    ax_h = inner * 0.62
    shaft_w = ax_w * 0.52
    head_h = ax_h * 0.42
    cx_up = pad + inner * 0.30
    cx_dn = pad + inner * 0.70
    cy = size / 2
    green = (0x34, 0xD3, 0x99, 255)
    blue = (0x38, 0xBD, 0xF8, 255)
    up = [
        (cx_up - shaft_w / 2, cy + ax_h / 2),
        (cx_up - shaft_w / 2, cy - ax_h / 2 + head_h),
        (cx_up - ax_w / 2, cy - ax_h / 2 + head_h),
        (cx_up, cy - ax_h / 2),
        (cx_up + ax_w / 2, cy - ax_h / 2 + head_h),
        (cx_up + shaft_w / 2, cy - ax_h / 2 + head_h),
        (cx_up + shaft_w / 2, cy + ax_h / 2),
    ]
    d.polygon(up, fill=green)
    dn = [
        (cx_dn - shaft_w / 2, cy - ax_h / 2),
        (cx_dn - shaft_w / 2, cy + ax_h / 2 - head_h),
        (cx_dn - ax_w / 2, cy + ax_h / 2 - head_h),
        (cx_dn, cy + ax_h / 2),
        (cx_dn + ax_w / 2, cy + ax_h / 2 - head_h),
        (cx_dn + shaft_w / 2, cy + ax_h / 2 - head_h),
        (cx_dn + shaft_w / 2, cy - ax_h / 2),
    ]
    d.polygon(dn, fill=blue)
    return im


def draw_sysmeter(size=SS):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pad = size * 0.03
    r = size * 0.22
    bw = int(size * 0.045)
    d.rounded_rectangle([pad, pad, size - pad, size - pad], radius=r,
                        fill=(0x0E, 0x14, 0x1B, 255), outline=(0x22, 0xD3, 0xEE, 255), width=bw)
    inner = size - 2 * pad
    base = pad + inner * 0.80
    bar_w = inner * 0.16
    gap = inner * 0.075
    total = 3 * bar_w + 2 * gap
    x0 = pad + (inner - total) / 2
    bars = [
        (0x22, 0xC5, 0x5E, 255, 0.42),
        (0xF5, 0x9E, 0x0B, 255, 0.68),
        (0x22, 0xD3, 0xEE, 255, 0.50),
    ]
    for i, (cr, cg, cb, ca, hfrac) in enumerate(bars):
        x = x0 + i * (bar_w + gap)
        h = inner * hfrac
        top = base - h
        d.rounded_rectangle([x, top, x + bar_w, base], radius=int(bar_w * 0.28),
                            fill=(cr, cg, cb, ca))
    return im


# Manifest file name -> tile file name produced by save_tiles()
MANIFEST_NAMES = {
    "Square150x150Logo.png": "tile-150.png",
    "Square44x44Logo.png": "tile-44.png",
    "Square71x71Logo.png": "tile-71.png",
    "Square310x310Logo.png": "tile-310.png",
    "Wide310x150Logo.png": "tile-wide-310x150.png",
    "StoreLogo.png": "logo-50.png",
    "SplashScreen.png": "splash-620x300.png",
}


def save_tiles(app, master, font_path):
    """Produce the full tile set, then copy them into Assets/ with the
    exact names AppxManifest.xml expects. Also keeps icon-512/300."""
    d = os.path.join(OUT, app)
    os.makedirs(d, exist_ok=True)
    for name, s in [("icon-512", 512), ("icon-300", 300), ("tile-310", 310),
                    ("tile-150", 150), ("tile-71", 71), ("tile-44", 44),
                    ("logo-50", 50)]:
        master.resize((s, s), Image.LANCZOS).save(os.path.join(d, f"{name}.png"))
    # wide tile 310x150: dark bg, icon left, name right
    wide = Image.new("RGBA", (310, 150), (0x0A, 0x0F, 0x14, 255))
    wd = ImageDraw.Draw(wide)
    icon_small = master.resize((80, 80), Image.LANCZOS)
    wide.paste(icon_small, (14, 35), icon_small)
    title = "NetSpeedMeter by GrafiGrow" if app == "netspeedmeter" else "SysMeter by GrafiGrow"
    tx, max_w = 108, 310 - 108 - 14
    if app == "netspeedmeter":
        # full name cannot fit on one line at a legible size — two lines
        f1 = ImageFont.truetype(font_path, 20)
        f2 = ImageFont.truetype(font_path, 20)
        wd.text((tx, 49), "NetSpeedMeter", font=f1, fill=(255, 255, 255, 255))
        wd.text((tx, 75), "by GrafiGrow", font=f2, fill=(255, 255, 255, 255))
    else:
        f1 = ImageFont.truetype(font_path, 20)
        f2 = ImageFont.truetype(font_path, 20)
        wd.text((tx, 49), "SysMeter", font=f1, fill=(255, 255, 255, 255))
        wd.text((tx, 75), "by GrafiGrow", font=f2, fill=(255, 255, 255, 255))
    wide.save(os.path.join(d, "tile-wide-310x150.png"))
    # splash 620x300: dark bg, icon centred, name below — auto-fit one line
    splash = Image.new("RGBA", (620, 300), (0x0A, 0x0F, 0x14, 255))
    sd = ImageDraw.Draw(splash)
    icon_mid = master.resize((140, 140), Image.LANCZOS)
    splash.paste(icon_mid, (240, 30), icon_mid)
    f2_size = 44
    while f2_size > 20:
        f2 = ImageFont.truetype(font_path, f2_size)
        if sd.textlength(title, font=f2) <= 560:
            break
        f2_size -= 1
    tw = sd.textlength(title, font=f2)
    sd.text(((620 - tw) / 2, 190), title, font=f2, fill=(255, 255, 255, 255))
    f3 = ImageFont.truetype(font_path, 22)
    sub = "Taskbar Speed Monitor" if app == "netspeedmeter" else "Taskbar CPU, RAM & Disk Monitor"
    tw3 = sd.textlength(sub, font=f3)
    sd.text(((620 - tw3) / 2, 245), sub, font=f3, fill=(0xC7, 0xD0, 0xD8, 255))
    splash.save(os.path.join(d, "splash-620x300.png"))
    # Copy into Assets/ with manifest names. Open the wide tile and splash
    # and check the name is fully inside the frame before finishing.
    assets = os.path.join(OUT, "Assets")
    os.makedirs(assets, exist_ok=True)
    for manifest_name, tile_name in MANIFEST_NAMES.items():
        shutil.copyfile(os.path.join(d, tile_name), os.path.join(assets, manifest_name))
    print("built", app, "->", assets)
    print("Check visually before packing: open Assets/Wide310x150Logo.png and")
    print("Assets/SplashScreen.png — the full name must not be clipped.")


def main():
    app = (sys.argv[1] if len(sys.argv) > 1 else "netspeedmeter").lower()
    if app not in ("netspeedmeter", "sysmeter"):
        raise SystemExit("usage: python build_icons.py [netspeedmeter|sysmeter]")
    font = find_font()
    print("font:", font)
    master = (draw_netspeedmeter() if app == "netspeedmeter" else draw_sysmeter())
    master = master.resize((512, 512), Image.LANCZOS)
    save_tiles(app, master, font)


if __name__ == "__main__":
    main()
