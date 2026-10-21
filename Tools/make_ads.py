# Makes the ad art for the board ads and ice logos (#87, real logos #223) as PNGs, one set per brand, plus the League
# Skin's centre-ice logo. The game (the RinkAds class) puts them on the boards and the ice at the Rink Layout's ad panel
# slots and ice logo spots, and picks the brands for each game load (the Brands class). The brand ids are the rows of the
# sources list (Assets/Resources/Ads/sources.csv: id, brand, category, source_url, file_url, notes) and the SVG file
# names: keep them in step with the Brands class.
#   panel-<id>.png       the brand's logo in its own colours, centred on a 3:1 panel. RGBA: clear round the logo, so it
#                        also lies on the ice (the logos beside the centre circle). The clear part is board white, so on
#                        the boards (an opaque material) the panel is plain white round the logo.
#   panel-<id>-soft.png  the same panel for the Calm Area: white with the logo in one soft, low-contrast grey (RGB).
#   round-<id>.png       a round logo for the ice by the neutral-zone dots: a white disc with a ring in the logo's main
#                        colour and the logo inside (RGBA, clear outside the circle).
#   centre-ice-all-star-hockey.png   the League Skin's "All-Star Hockey" placeholder mark for centre ice (RGBA).
# Text and texture "up" are the PNG's top; the game lays them so they read from the ice (boards) and from behind the
# near net looking out (ice logos, the Replay view).
# The .meta files keep each picture's own size (Non-Power of 2: None), clamp its edges and filter it at a slant
# (Aniso 8), for the ice logos seen low across the ice. A new PNG gets a copy of the centre-ice logo's .meta (new guid).
# SVGs are rasterised by headless Chrome (or Edge): each SVG is shown in an <img> (no scripts run) on a clear page and
# screenshotted big, then cropped to the logo. The SVGs are downloaded files: run this with python -I.
# Fonts: Windows' own TrueType fonts (the centre-ice mark), from the fonts folder given.
# Needs Pillow and numpy (pip install -r Tools/requirements.txt) and Chrome or Edge.
# Run: py -3 -I Tools/make_ads.py Assets/Resources/Ads C:/Windows/Fonts <folder of <id>.svg> Assets/Resources/Ads/sources.csv
import csv, math, os, re, shutil, subprocess, sys, tempfile, uuid
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

out, fonts, svgs, sources = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
os.makedirs(out, exist_ok=True)

SS = 4                                   # drawn this many times bigger, then shrunk, for smooth edges
PANEL = (768, 256)                       # 3:1, narrower than the narrowest panel (2.44 x 0.73 m), so the logo always fits
PANEL_FIT = (0.84, 0.70)                 # the logo's largest share of the panel's width and height
ROUND = 512
ROUND_FIT = 0.36                         # the logo's corners stay this far (share of the disc) from the disc's centre
CENTRE = 1024
RENDER = 2400                            # px: the SVG's longer side when rasterised, before it is shrunk
BOARD_WHITE = (236, 238, 240)            # the boards' white (Tools/make_arena.py, "Boards")
SOFT_GREY = (219, 222, 225)              # Calm Area logos: about 6% darker than the white, no colour
SOFT_BLUR = 2.0                          # px: soft edges on the Calm Area logos
# Logos drawn on a full-size white box (Garrett, #223): the white joined to the picture's edge is cleared.
WHITE_BOX = {"michelob-ultra", "shell", "pizza-pizza", "cibc", "discover"}
BROWSERS = [r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"]


class Canvas:
    """An RGB picture on a background colour plus its coverage mask (the logo's shape), drawn SS times bigger."""

    def __init__(self, size, background):
        self.size = (size[0] * SS, size[1] * SS)
        self.rgb = Image.new("RGB", self.size, background)
        self.mask = Image.new("L", self.size, 0)
        self.d, self.m = ImageDraw.Draw(self.rgb), ImageDraw.Draw(self.mask)

    def polygon(self, points, colour):
        pts = [(x * SS, y * SS) for x, y in points]
        self.d.polygon(pts, fill=colour); self.m.polygon(pts, fill=255)

    def ellipse(self, box, colour):
        b = [v * SS for v in box]
        self.d.ellipse(b, fill=colour); self.m.ellipse(b, fill=255)

    def text(self, centre, text, font_file, colour, box_w, box_h):
        """Text fitted into a box_w x box_h box, centred on centre."""
        font = fitted(font_file, text, box_w * SS, box_h * SS)
        x0, y0, x1, y1 = font.getbbox(text)
        at = (centre[0] * SS - (x0 + x1) / 2, centre[1] * SS - (y0 + y1) / 2)
        self.d.text(at, text, font=font, fill=colour); self.m.text(at, text, font=font, fill=255)
        return (x1 - x0) / SS

    def shrink(self):
        small = (self.size[0] // SS, self.size[1] // SS)
        return self.rgb.resize(small, Image.LANCZOS), self.mask.resize(small, Image.LANCZOS)


def fitted(font_file, text, w, h):
    size = int(h)
    while size > 4:
        font = ImageFont.truetype(os.path.join(fonts, font_file), size)
        x0, y0, x1, y1 = font.getbbox(text)
        if x1 - x0 <= w and y1 - y0 <= h: return font
        size = int(size * 0.95)
    raise ValueError(f"{text!r} does not fit")


def rgba(rgb, mask):
    out = rgb.copy()
    out.putalpha(mask)
    return out


def soft(mask):
    """The Calm Area panel: board white with the logo's shape in one soft grey, edges blurred."""
    shape = mask.filter(ImageFilter.GaussianBlur(SOFT_BLUR))
    return Image.composite(Image.new("RGB", mask.size, SOFT_GREY), Image.new("RGB", mask.size, BOARD_WHITE), shape)


# ---------- SVG to a cropped RGBA logo ----------

def browser():
    for b in BROWSERS:
        if os.path.exists(b): return b
    raise SystemExit("needs Chrome or Edge: " + " or ".join(BROWSERS))


def svg_size(text):
    """The SVG's (width, height) from its viewBox, else its width and height; and whether it has a viewBox."""
    tag = re.search(r"<svg\b[^>]*>", text, re.S)
    if not tag: raise ValueError("no <svg> tag")
    tag = tag.group(0)
    box = re.search(r"\bviewBox\s*=\s*[\"']([^\"']+)[\"']", tag)
    if box:
        v = [float(x) for x in re.split(r"[\s,]+", box.group(1).strip())]
        return v[2], v[3], True
    w = re.search(r"\bwidth\s*=\s*[\"']([\d.]+)", tag)
    h = re.search(r"\bheight\s*=\s*[\"']([\d.]+)", tag)
    return (float(w.group(1)) if w else 300.0), (float(h.group(1)) if h else 150.0), False


def rasterise(svg, work):
    """The SVG as a big RGBA picture on a clear background, cropped to the logo."""
    with open(svg, encoding="utf-8", errors="replace") as f:
        w, h, scalable = svg_size(f.read())
    url = "file:///" + os.path.abspath(svg).replace("\\", "/")
    if scalable:   # fills the page, keeping its shape
        scale, page = 1.0, (RENDER, RENDER)
        style = f"width:{RENDER}px;height:{RENDER}px;object-fit:contain"
    else:          # no viewBox: drawn at its own size, the page zoomed up
        scale = RENDER / max(w, h)
        page = (math.ceil(w) + 2, math.ceil(h) + 2)
        style = ""
    html = os.path.join(work, "page.html")
    with open(html, "w", encoding="utf-8") as f:
        f.write(f'<!DOCTYPE html><html><body style="margin:0;background:transparent">'
                f'<img src="{url}" style="display:block;{style}"></body></html>')
    shot = os.path.join(work, "shot.png")
    if os.path.exists(shot): os.remove(shot)
    subprocess.run([browser(), "--headless", "--mute-audio", "--disable-gpu", "--hide-scrollbars", "--no-first-run",
                    "--user-data-dir=" + os.path.join(work, "profile"), "--default-background-color=00000000",
                    f"--force-device-scale-factor={scale}", f"--window-size={page[0]},{page[1]}",
                    "--screenshot=" + shot, "file:///" + html.replace("\\", "/")],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
    return crop(Image.open(shot).convert("RGBA"))


def crop(picture):
    box = picture.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    if not box: raise ValueError("empty logo")
    return picture.crop(box)


def clear_white_box(logo):
    """Clears the white (and clear) area joined to the picture's edge: a full-size white box behind the logo."""
    a = np.asarray(logo).astype(np.int32)
    white = ((a[..., 3] < 30) | ((a[..., 0] > 225) & (a[..., 1] > 225) & (a[..., 2] > 225))).astype(np.uint8) * 255
    fill = Image.fromarray(np.pad(white, 1, constant_values=255).astype(np.uint8), "L").copy()  # a copy: floodfill leaves an array-backed picture unchanged
    ImageDraw.floodfill(fill, (0, 0), 128)
    joined = np.asarray(fill)[1:-1, 1:-1] == 128
    a[joined, 3] = 0
    return crop(Image.fromarray(a.astype(np.uint8), "RGBA"))


def main_colour(logo):
    """The logo's main colour: its commonest strong colour, else its commonest dark one."""
    a = np.asarray(logo).reshape(-1, 4).astype(np.int32)
    a = a[a[:, 3] > 200][:, :3]
    sat = a.max(axis=1) - a.min(axis=1)
    pick = a[sat > 64] if (sat > 64).sum() > len(a) * 0.05 else a[a.sum(axis=1) < 600]
    if len(pick) == 0: return (40, 40, 45)
    q = (pick // 16) * 16 + 8
    keys, counts = np.unique(q, axis=0, return_counts=True)
    return tuple(int(v) for v in keys[counts.argmax()])


def on_white(logo, size):
    """The logo laid on board white at size: (RGB, alpha)."""
    rgb = Image.new("RGB", logo.size, BOARD_WHITE)
    rgb.paste(logo, (0, 0), logo)
    return rgb.resize(size, Image.LANCZOS), logo.getchannel("A").resize(size, Image.LANCZOS)


def panel(logo):
    """The logo centred on a 3:1 board-white panel, its shape kept: (RGB, mask)."""
    w, h = PANEL
    k = min(w * PANEL_FIT[0] / logo.width, h * PANEL_FIT[1] / logo.height)
    size = (max(1, round(logo.width * k)), max(1, round(logo.height * k)))
    rgb, alpha = on_white(logo, size)
    at = ((w - size[0]) // 2, (h - size[1]) // 2)
    canvas, mask = Image.new("RGB", PANEL, BOARD_WHITE), Image.new("L", PANEL, 0)
    canvas.paste(rgb, at); mask.paste(alpha, at)
    return canvas, mask


def round_logo(logo):
    """A white disc with a ring in the logo's main colour and the logo inside."""
    n = ROUND
    c = Canvas((n, n), BOARD_WHITE)
    c.ellipse((n * 0.02, n * 0.02, n * 0.98, n * 0.98), main_colour(logo))
    c.ellipse((n * 0.06, n * 0.06, n * 0.94, n * 0.94), BOARD_WHITE)
    rgb, mask = c.shrink()
    k = n * ROUND_FIT / math.hypot(logo.width / 2, logo.height / 2)
    size = (max(1, round(logo.width * k)), max(1, round(logo.height * k)))
    small, alpha = on_white(logo, size)
    rgb.paste(small, ((n - size[0]) // 2, (n - size[1]) // 2), alpha)
    return rgba(rgb, mask)


def centre_ice():
    """The League Skin's placeholder mark: a navy crest with a gold ring, a white star and ALL-STAR HOCKEY."""
    n = CENTRE
    navy, gold, white = (12, 35, 80), (215, 165, 40), (255, 255, 255)
    c = Canvas((n, n), BOARD_WHITE)
    c.ellipse((n * 0.01, n * 0.01, n * 0.99, n * 0.99), gold)
    c.ellipse((n * 0.05, n * 0.05, n * 0.95, n * 0.95), navy)
    star = [(n / 2 + n * (0.16 if k % 2 == 0 else 0.065) * math.sin(math.pi * k / 5),
             n * 0.47 - n * (0.16 if k % 2 == 0 else 0.065) * math.cos(math.pi * k / 5)) for k in range(10)]
    c.polygon(star, white)
    c.text((n / 2, n * 0.21), "ALL-STAR", "ariblk.ttf", white, n * 0.5, n * 0.08)
    c.text((n / 2, n * 0.74), "HOCKEY", "ariblk.ttf", gold, n * 0.62, n * 0.13)
    return rgba(*c.shrink())


META_FROM = "centre-ice-all-star-hockey.png"


def save(picture, name):
    """Saves the PNG, and a .meta (a copy of the centre-ice logo's, new guid) if it has none."""
    path = os.path.join(out, name)
    picture.save(path, optimize=True)
    meta, template = path + ".meta", os.path.join(out, META_FROM + ".meta")
    if not os.path.exists(meta) and os.path.exists(template):
        with open(template, encoding="utf-8", newline="") as f:
            text = f.read()
        with open(meta, "w", encoding="utf-8", newline="") as f:
            f.write(re.sub(r"(?m)^guid: [0-9a-f]+", "guid: " + uuid.uuid4().hex, text, count=1))


save(centre_ice(), META_FROM)
with open(sources, encoding="utf-8", newline="") as f:
    ids = [row["id"] for row in csv.DictReader(f)]
only = set(sys.argv[5:])                 # optional: just these ids (a quick look)
work = tempfile.mkdtemp(prefix="make_ads_")
try:
    for i in ids:
        if only and i not in only: continue
        logo = rasterise(os.path.join(svgs, i + ".svg"), work)
        if i in WHITE_BOX: logo = clear_white_box(logo)
        rgb, mask = panel(logo)
        save(rgba(rgb, mask), f"panel-{i}.png")
        save(soft(mask), f"panel-{i}-soft.png")
        save(round_logo(logo), f"round-{i}.png")
        print(i, logo.size)
finally:
    shutil.rmtree(work, ignore_errors=True)
print(len(ids), "brands written to", out)
