# Writes goalie-skin.png: Kenney's CC0 skaterMaleA.png skin with the shirt repainted as a goalie jersey
# (blue with white stripes and a white crest) and the jeans repainted as dark hockey pants.
# Run: python Tools/make_goalie_skin.py <path to Kenney Skins/skaterMaleA.png> Assets/Resources/Models
# Needs only the Python standard library (zlib, struct). Reads and writes 8-bit, non-interlaced RGB/RGBA PNGs.
import os, struct, sys, zlib

JERSEY = (38, 64, 153)      # the block goalie's blue (0.15, 0.25, 0.6)
STRIPE = (245, 245, 245)
PANTS = (22, 26, 52)


def read_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not a PNG"
    pos, idat, w = 8, b"", None
    while pos < len(data):
        n, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        pos += 12 + n
        if kind == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", body)
            assert depth == 8 and ctype in (2, 6) and interlace == 0, "unsupported PNG"
        elif kind == b"IDAT":
            idat += body
        elif kind == b"IEND":
            break
    bpp = 4 if ctype == 6 else 3
    raw = zlib.decompress(idat)
    stride = w * bpp
    rows, prev = [], bytearray(stride)
    for y in range(h):
        f = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    # to RGBA rows
    px = []
    for line in rows:
        out = bytearray()
        for x in range(w):
            out += line[x * bpp:x * bpp + 3] + (bytes([line[x * bpp + 3]]) if bpp == 4 else b"\xff")
        px.append(out)
    return w, h, px


def write_png(path, w, h, px):
    raw = b"".join(b"\x00" + bytes(row) for row in px)
    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xffffffff)
    with open(path, "wb") as o:
        o.write(b"\x89PNG\r\n\x1a\n")
        o.write(chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)))
        o.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        o.write(chunk(b"IEND", b""))


def fill(px, x0, y0, x1, y1, rgb, s):
    for y in range(int(y0 * s), int(y1 * s)):
        row = px[y]
        for x in range(int(x0 * s), int(x1 * s)):
            row[x * 4:x * 4 + 4] = bytes(rgb) + b"\xff"


def disc(px, cx, cy, r, rgb, s):
    for y in range(int((cy - r) * s), int((cy + r) * s)):
        for x in range(int((cx - r) * s), int((cx + r) * s)):
            if (x / s - cx) ** 2 + (y / s - cy) ** 2 <= r * r:
                px[y][x * 4:x * 4 + 4] = bytes(rgb) + b"\xff"


def main():
    src, out_dir = sys.argv[1], sys.argv[2]
    w, h, px = read_png(src)
    s = w / 1024.0  # layout below is in 1024 px skin units
    # Shirt: torso strip (152..490) and both sleeves (0..152, 490..611), rows 490..1024.
    fill(px, 0, 490, 611, 1024, JERSEY, s)
    for y0, y1 in ((880, 910), (935, 950)):          # hem stripes across torso and sleeves
        fill(px, 0, y0, 611, y1, STRIPE, s)
    for y0, y1 in ((600, 625), (645, 655)):          # sleeve stripes
        fill(px, 0, y0, 152, y1, STRIPE, s)
        fill(px, 490, y0, 611, y1, STRIPE, s)
    disc(px, 321, 700, 55, STRIPE, s)                # chest crest
    disc(px, 321, 700, 38, JERSEY, s)
    # Jeans -> hockey pants (the pads cover most of the leg).
    fill(px, 611, 764, 1024, 1024, PANTS, s)
    os.makedirs(out_dir, exist_ok=True)
    write_png(os.path.join(out_dir, "goalie-skin.png"), w, h, px)
    print("wrote", os.path.join(out_dir, "goalie-skin.png"), w, "x", h)


main()
