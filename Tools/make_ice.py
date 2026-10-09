# Writes the ice model ice.obj + ice.mtl: one flat 60 m x 60 m sheet, top at y = 0, centred on the Net Face
# (x = 0, z = 0), textured with ice-markings.jpg (made by Tools/make_ice_texture.py: ice, rink lines and crease).
# Unity mirrors OBJ files on x when it imports them, so u runs from file +x to file -x: in the game the
# texture's left edge is at world x = -30 and its top edge is at world z = +30 (behind the net).
# Run: python Tools/make_ice.py Assets/Resources/Models
import os, sys

out = sys.argv[1]
HALF = 30.0

corners = [(-HALF, -HALF), (-HALF, HALF), (HALF, HALF), (HALF, -HALF)]   # (file x, z), wound to face +Y
with open(os.path.join(out, "ice.obj"), "w") as o:
    o.write("# Hockey ice sheet, made for Hockey Game Test by Tools/make_ice.py. CC0.\nmtllib ice.mtl\n")
    for x, z in corners: o.write(f"v {x:.3f} 0 {z:.3f}\n")
    for x, z in corners: o.write(f"vt {(HALF - x) / (2 * HALF):.3f} {(z + HALF) / (2 * HALF):.3f}\n")
    o.write("vn 0 1 0\n")
    # Use "g" (not "o"): Unity names the part after the group.
    o.write("g Ice\nusemtl Ice\ns off\nf 1/1/1 2/2/1 3/3/1\nf 1/1/1 3/3/1 4/4/1\n")
with open(os.path.join(out, "ice.mtl"), "w") as o:
    o.write("# Hockey ice material. CC0.\nnewmtl Ice\nKd 1 1 1\nKs 0.15 0.15 0.15\nNs 40\nmap_Kd ice-markings.jpg\n")
print("ice.obj written")
