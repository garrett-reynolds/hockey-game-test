# Writes the water bottle model as OBJ+MTL and its body picture as PNG, in metres, Y up (#163).
#   py -3 Tools/make_water_bottle.py Assets/Resources/Models
# A Gatorade-style squeeze bottle (Garrett's reference picture, personal project: the logo is fine): a green body 7.5 cm wide with a
# grip waist, a silver-grey swoosh band and the white "G" with the orange bolt on the front, an orange cap with a ridged ring and a
# pull-up nozzle. 25 cm tall in all, base at y = 0, standing on its axis (the game lays it on its side). Each part is a lathe (a
# profile turned round the Y axis). The body's UVs run round (u) and up (v) its picture, bottle-body.png (the game puts that picture
# on the body's material); the cap and nozzle are one plain orange.
# Needs Pillow and Windows' Arial Black or Arial Bold (as make_ads.py).
import math, os, sys

SEGMENTS = 32
BODY_TOP = 0.186

# (radius, height) points from the bottom up. The body is closed at the bottom by a point on the axis.
BODY = [(0.0, 0.0), (0.030, 0.0), (0.0365, 0.006), (0.0375, 0.016), (0.0375, 0.050), (0.0352, 0.072), (0.0345, 0.092),
        (0.0352, 0.112), (0.0375, 0.134), (0.0372, 0.146), (0.0355, 0.158), (0.0285, 0.172), (0.0190, 0.181), (0.0170, BODY_TOP)]
CAP = [(0.0170, BODY_TOP), (0.0235, 0.188), (0.0235, 0.192), (0.0212, 0.193), (0.0212, 0.196), (0.0235, 0.197), (0.0235, 0.201),
       (0.0212, 0.202), (0.0212, 0.205), (0.0185, 0.208), (0.0140, 0.210)]
NOZZLE = [(0.0140, 0.210), (0.0115, 0.214), (0.0110, 0.238), (0.0090, 0.246), (0.0050, 0.250), (0.0, 0.250)]

# (part, material, profile, has body picture)
PARTS = [("Body", "Bottle Body", BODY), ("Cap", "Cap Orange", CAP), ("Nozzle", "Cap Orange", NOZZLE)]
MATERIALS = [("Bottle Body", (0.05, 0.62, 0.18), 60), ("Cap Orange", (0.95, 0.30, 0.02), 40)]
PICTURE = "bottle-body.png"
PICTURE_SIZE = (1024, 800)   # round x up: the body is 0.236 m round and 0.186 m tall


def lathe(profile, first_vertex, top):
    """Vertices, UVs and outward-facing triangles for the profile turned round Y. u = angle / 360, v = height / top."""
    verts, uvs, tris, rings = [], [], [], []
    for r, y in profile:
        if r == 0.0:
            rings.append([len(verts)] * (SEGMENTS + 1))
            verts.append((0.0, y, 0.0)); uvs.append((0.5, y / top))
        else:
            rings.append([len(verts) + i for i in range(SEGMENTS + 1)])
            for i in range(SEGMENTS + 1):   # one more round the seam, so the picture does not smear across it
                t = 2 * math.pi * i / SEGMENTS
                verts.append((r * math.cos(t), y, r * math.sin(t)))
                uvs.append((i / SEGMENTS, y / top))
    for a, b in zip(rings, rings[1:]):
        for i in range(SEGMENTS):
            j = i + 1
            # Counter-clockwise seen from outside (OBJ is right-handed); Unity's importer turns it into its own front faces.
            if a[0] == a[1]:      # fan from the bottom point
                tris.append((a[i], b[i], b[j]))
            elif b[0] == b[1]:    # fan to the top point
                tris.append((a[i], b[0], a[j]))
            else:
                tris.append((a[i], b[j], a[j]))
                tris.append((a[i], b[i], b[j]))
    return verts, uvs, [tuple(k + first_vertex + 1 for k in t) for t in tris]


def picture(path):
    """The body picture: green, a wide silver-grey diagonal swoosh and the white G with the orange bolt, centred on u = 0.75 (the
    side the shooter sees: angle 270 degrees = -z when the bottle lies with its axis along x). Image y runs down, v runs up."""
    from PIL import Image, ImageDraw, ImageFont
    w, h = PICTURE_SIZE
    green, grey, orange, white = (13, 158, 46), (176, 182, 178), (245, 128, 25), (255, 255, 255)
    img = Image.new("RGB", (w, h), green)
    d = ImageDraw.Draw(img)
    # The swoosh: an S-curved band from the lower left of the front to the upper right, about 0.1 m wide on the bottle.
    left, right = [], []
    for k in range(41):
        s = k / 40
        cx = 0.42 * w + 0.36 * w * s + 0.05 * w * math.sin(2 * math.pi * s)
        y = h * (0.98 - 0.96 * s)
        half = 0.075 * w * (0.8 + 0.4 * math.sin(math.pi * s))
        left.append((cx - half, y)); right.append((cx + half, y))
    d.polygon(left + right[::-1], fill=grey)
    # The G, white with a dark green edge, a little left of the middle of the front; the bolt crosses its mouth.
    cx, cy = int(0.75 * w), int(0.5 * h)
    font = None
    for name in ("ariblk.ttf", "arialbd.ttf"):
        try:
            font = ImageFont.truetype(os.path.join("C:/Windows/Fonts", name), 380)
            break
        except OSError:
            pass
    if font is None:
        sys.exit("needs Arial Black or Arial Bold in C:/Windows/Fonts")
    d.text((cx - 20, cy), "G", font=font, fill=white, anchor="mm", stroke_width=14, stroke_fill=(10, 90, 30))
    d.text((cx - 20, cy), "G", font=font, fill=white, anchor="mm")
    bolt = [(cx + 40, cy - 150), (cx + 150, cy - 150), (cx + 95, cy - 40), (cx + 175, cy - 40), (cx + 10, cy + 150), (cx + 50, cy + 10),
            (cx - 20, cy + 10)]
    d.polygon(bolt, fill=orange, outline=white, width=7)
    img.save(path)


def main(folder):
    name = "bottle"
    os.makedirs(folder, exist_ok=True)
    all_verts, all_uvs, parts = [], [], []
    for part, material, profile in PARTS:
        verts, uvs, tris = lathe(profile, len(all_verts), BODY_TOP)
        all_verts += verts
        all_uvs += uvs
        parts.append((part, material, tris))
    with open(os.path.join(folder, name + ".obj"), "w", newline="\n") as o:
        o.write(f"# Water bottle. Made for Hockey Game Test by Tools/make_water_bottle.py. CC0.\nmtllib {name}.mtl\n")
        for x, y, z in all_verts:
            o.write(f"v {x:.5f} {y:.5f} {z:.5f}\n")
        for u, v in all_uvs:
            o.write(f"vt {u:.5f} {v:.5f}\n")
        for part, material, tris in parts:
            o.write(f"g {part}\nusemtl {material}\n")
            for a, b, c in tris:
                o.write(f"f {a}/{a} {b}/{b} {c}/{c}\n")
    with open(os.path.join(folder, name + ".mtl"), "w", newline="\n") as o:
        o.write("# Water bottle materials. CC0.\n")
        for material, (r, g, b), ns in MATERIALS:
            o.write(f"newmtl {material}\nKd {r} {g} {b}\nKs 0.2 0.2 0.2\nNs {ns}\n")
    picture(os.path.join(folder, PICTURE))
    print("BOTTLE wrote", os.path.join(folder, name + ".obj"), "triangles", sum(len(t) for _, _, t in parts))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("usage: make_water_bottle.py <output folder>")
    main(sys.argv[1])
