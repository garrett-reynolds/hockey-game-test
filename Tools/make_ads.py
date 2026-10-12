# Makes the ad art for the board ads and ice logos (#87) as PNGs, one set per made-up brand, plus the League Skin's
# centre-ice logo. The game (RinkAds.cs) puts them on the boards and the ice at the Rink Layout's ad panel slots and ice
# logo spots, and picks the brand for each (Brands.cs). The brand ids below are the art names RinkAds loads: keep them
# in step with Brands.cs.
#   panel-<id>.png       the brand's logo in its colours, 3:1. RGBA: clear round the logo, so it also lies on the ice
#                        (the logos beside the centre circle). The clear part is board white, so on the boards (an
#                        opaque material) the panel is plain white round the logo.
#   panel-<id>-soft.png  the same panel for the Calm Area: white with the logo in one soft, low-contrast grey (RGB).
#   round-<id>.png       a round logo for the ice by the neutral-zone dots (RGBA, clear outside the circle).
#   centre-ice-all-star-hockey.png   the League Skin's "All-Star Hockey" placeholder mark for centre ice (RGBA).
# Text and texture "up" are the PNG's top; the game lays them so they read from the ice (boards) and from behind the
# near net looking out (ice logos, the Replay view).
# The .meta files keep each picture's own size (Non-Power of 2: None), clamp its edges and filter it at a slant
# (Aniso 8), for the ice logos seen low across the ice: keep those settings when adding art.
# Fonts: Windows' own TrueType fonts (Georgia, Arial, Impact, Segoe UI, ...), from the fonts folder given.
# Needs Pillow (pip install pillow).
# Run: py -3 Tools/make_ads.py Assets/Resources/Ads C:/Windows/Fonts
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

out, fonts = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)

SS = 4                                   # drawn this many times bigger, then shrunk, for smooth edges
PANEL = (768, 256)                       # 3:1, narrower than the narrowest panel (2.44 x 0.73 m), so the logo always fits
ROUND = 512
CENTRE = 1024
BOARD_WHITE = (236, 238, 240)            # the boards' white (Tools/make_arena.py, "Boards")
SOFT_GREY = (219, 222, 225)              # Calm Area logos: about 6% darker than the white, no colour
SOFT_BLUR = 2.0                          # px: soft edges on the Calm Area logos


# id, name, wordmark, font, main colour, second colour, mark
BRANDS = [
    ("northvale-bank", "Northvale Bank", "NORTHVALE BANK", "georgiab.ttf", (12, 35, 80), (255, 255, 255), "monogram N"),
    ("glacierlink", "GlacierLink", "GlacierLink", "seguibl.ttf", (110, 40, 170), (120, 200, 240), "crystal"),
    ("brawnys-lager", "Brawny's Lager", "BRAWNY'S LAGER", "ROCKB.TTF", (110, 45, 15), (215, 160, 30), "oval B"),
    ("kestrel-motors", "Kestrel Motors", "KESTREL MOTORS", "FRAMDCN.TTF", (30, 30, 35), (190, 30, 40), "wings"),
    ("aurora-air", "Aurora Air", "Aurora Air", "trebucbd.ttf", (0, 125, 135), (90, 200, 120), "arcs"),
    ("shieldcross", "Shieldcross", "SHIELDCROSS", "ariblk.ttf", (0, 90, 60), (255, 255, 255), "shield"),
    ("volt-rush", "Volt Rush", "VOLT RUSH", "impact.ttf", (20, 20, 20), (170, 230, 0), "bolt"),
    ("big-timber-burgers", "Big Timber Burgers", "BIG TIMBER BURGERS", "ROCKB.TTF", (220, 95, 15), (120, 60, 20), "burger"),
    ("peakside-energy", "Peakside Energy", "PEAKSIDE ENERGY", "verdanab.ttf", (0, 80, 170), (250, 180, 0), "peaks"),
    ("frostbyte-pcs", "FrostByte PCs", "FrostByte PCs", "bahnschrift.ttf", (0, 150, 205), (20, 40, 70), "pixels"),
]


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

    def rect(self, box, colour, radius=0):
        b = [v * SS for v in box]
        self.d.rounded_rectangle(b, radius * SS, fill=colour); self.m.rounded_rectangle(b, radius * SS, fill=255)

    def arc(self, box, start, end, colour, width):
        b = [v * SS for v in box]
        self.d.arc(b, start, end, fill=colour, width=int(width * SS)); self.m.arc(b, start, end, fill=255, width=int(width * SS))

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


def mark(c, kind, cx, cy, r, main, second, font_file):
    """The brand's mark: a simple shape about 2r across, centred on (cx, cy)."""
    kind, _, letter = kind.partition(" ")
    if kind == "monogram":
        c.rect((cx - r, cy - r, cx + r, cy + r), main, radius=r * 0.25)
        c.text((cx, cy), letter, font_file, second, r * 1.3, r * 1.3)
    elif kind == "crystal":
        c.polygon([(cx, cy - r), (cx + r * 0.85, cy - r * 0.2), (cx + r * 0.45, cy + r), (cx - r * 0.45, cy + r), (cx - r * 0.85, cy - r * 0.2)], main)
        c.polygon([(cx, cy - r), (cx + r * 0.45, cy + r), (cx - r * 0.1, cy + r * 0.1)], second)
    elif kind == "oval":
        c.ellipse((cx - r, cy - r * 0.8, cx + r, cy + r * 0.8), second)
        c.ellipse((cx - r * 0.82, cy - r * 0.64, cx + r * 0.82, cy + r * 0.64), main)
        c.text((cx, cy), letter, font_file, second, r * 1.0, r * 0.9)
    elif kind == "wings":
        for k in range(3):
            y = cy - r * 0.7 + k * r * 0.55
            c.polygon([(cx - r, y), (cx, y + r * 0.45), (cx + r, y), (cx + r, y + r * 0.3), (cx, y + r * 0.75), (cx - r, y + r * 0.3)], main if k else second)
    elif kind == "arcs":
        for k, col in enumerate((main, second, main)):
            rr = r * (1.0 - k * 0.28)
            c.arc((cx - rr, cy - rr + r * 0.4, cx + rr, cy + rr + r * 0.4), 200, 340, col, r * 0.18)
    elif kind == "shield":
        c.polygon([(cx - r * 0.85, cy - r), (cx + r * 0.85, cy - r), (cx + r * 0.85, cy), (cx, cy + r), (cx - r * 0.85, cy)], main)
        c.rect((cx - r * 0.13, cy - r * 0.75, cx + r * 0.13, cy + r * 0.6), second)
        c.rect((cx - r * 0.6, cy - r * 0.35, cx + r * 0.6, cy - r * 0.09), second)
    elif kind == "bolt":
        c.rect((cx - r, cy - r, cx + r, cy + r), main, radius=r * 0.2)
        c.polygon([(cx + r * 0.25, cy - r * 0.85), (cx - r * 0.5, cy + r * 0.1), (cx - r * 0.02, cy + r * 0.1),
                   (cx - r * 0.25, cy + r * 0.85), (cx + r * 0.5, cy - r * 0.1), (cx + r * 0.02, cy - r * 0.1)], second)
    elif kind == "burger":
        c.polygon([(cx - r, cy - r * 0.1)] + [(cx - r * math.cos(a), cy - r * 0.1 - r * 0.8 * math.sin(a))
                                                for a in (math.pi * k / 16 for k in range(17))] + [(cx + r, cy - r * 0.1)], main)
        c.rect((cx - r, cy + r * 0.05, cx + r, cy + r * 0.35), second, radius=r * 0.1)
        c.rect((cx - r, cy + r * 0.5, cx + r, cy + r * 0.8), main, radius=r * 0.15)
    elif kind == "peaks":
        c.polygon([(cx - r, cy + r * 0.8), (cx - r * 0.3, cy - r * 0.6), (cx + r * 0.4, cy + r * 0.8)], main)
        c.polygon([(cx - r * 0.2, cy + r * 0.8), (cx + r * 0.35, cy - r), (cx + r, cy + r * 0.8)], main)
        c.ellipse((cx + r * 0.35, cy - r * 0.95, cx + r * 0.85, cy - r * 0.45), second)
    elif kind == "pixels":
        s = r * 0.6
        for i in range(3):
            for j in range(3):
                if (i + j) % 2 == 0:
                    c.rect((cx - r + i * (s + r * 0.1), cy - r + j * (s + r * 0.1), cx - r + i * (s + r * 0.1) + s, cy - r + j * (s + r * 0.1) + s), main if i == j else second, radius=s * 0.15)
    else:
        raise ValueError(kind)


def panel(brand):
    """The brand's logo: its mark, then its wordmark, centred on a 3:1 panel with a clear margin."""
    _, _, words, font_file, main, second, kind = brand
    w, h = PANEL
    c = Canvas(PANEL, BOARD_WHITE)
    r = h * 0.26
    text_h = h * 0.34
    # Measure the wordmark, then centre mark + gap + wordmark across the panel.
    probe = fitted(font_file, words, (w * 0.86 - 2.6 * r) * SS, text_h * SS)
    x0, _, x1, _ = probe.getbbox(words)
    total = 2 * r + 0.4 * r + (x1 - x0) / SS
    left = (w - total) / 2
    mark(c, kind, left + r, h / 2, r, main, second, font_file)
    c.text((left + 2.4 * r + (x1 - x0) / SS / 2, h / 2), words, font_file, main, w * 0.86 - 2.6 * r, text_h)
    return c.shrink()


def rgba(rgb, mask):
    out = rgb.copy()
    out.putalpha(mask)
    return out


def soft(mask):
    """The Calm Area panel: board white with the logo's shape in one soft grey, edges blurred."""
    shape = mask.filter(ImageFilter.GaussianBlur(SOFT_BLUR))
    return Image.composite(Image.new("RGB", mask.size, SOFT_GREY), Image.new("RGB", mask.size, BOARD_WHITE), shape)


def round_logo(brand):
    """A round logo: a disc in the brand's main colour with a ring, its mark and its first word in white."""
    _, _, words, font_file, main, second, kind = brand
    n = ROUND
    c = Canvas((n, n), BOARD_WHITE)
    c.ellipse((n * 0.02, n * 0.02, n * 0.98, n * 0.98), second if second != (255, 255, 255) else main)
    c.ellipse((n * 0.07, n * 0.07, n * 0.93, n * 0.93), main)
    white = (255, 255, 255)
    shape = kind.split(" ")[0]
    if shape in ("monogram", "shield"): fore, accent = white, main      # a white shape with the detail in the disc colour
    elif shape == "bolt": fore, accent = second, main
    else: fore, accent = white, second
    mark(c, kind, n / 2, n * 0.38, n * 0.17, fore, accent, font_file)
    first = words.split(" ")
    short = " ".join(first[:2]) if len(first[0]) < 4 else first[0]   # "BIG TIMBER", not just "BIG"
    c.text((n / 2, n * 0.7), short, font_file, white, n * 0.62, n * 0.14)
    return rgba(*c.shrink())


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


for b in BRANDS:
    rgb, mask = panel(b)
    rgba(rgb, mask).save(os.path.join(out, f"panel-{b[0]}.png"), optimize=True)
    soft(mask).save(os.path.join(out, f"panel-{b[0]}-soft.png"), optimize=True)
    round_logo(b).save(os.path.join(out, f"round-{b[0]}.png"), optimize=True)
centre_ice().save(os.path.join(out, "centre-ice-all-star-hockey.png"), optimize=True)
print(len(BRANDS), "brands written to", out)
