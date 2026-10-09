# Makes ice-detail.png, the faint skate-scuff texture tiled over the ice by Tools/make_ice.py.
# Source: ambientCG "Ice002" (CC0), https://ambientcg.com/view?id=Ice002 , file Ice002_1K-JPG_Roughness.jpg
# from https://ambientcg.com/get?file=Ice002_1K-JPG.zip . The roughness map is turned into a near-white
# grey (about 90-100% brightness) so the ice reads as white rink ice with light scuffs.
# Needs Pillow (pip install pillow).
# Run: python Tools/make_ice_detail.py <path>/Ice002_1K-JPG_Roughness.jpg Assets/Resources/Models
import os, sys
from PIL import Image, ImageOps

src, out = sys.argv[1], sys.argv[2]
grey = ImageOps.autocontrast(Image.open(src).convert("L"), cutoff=1)
lo, hi = 228, 255                                   # darkest scuff, clean ice
detail = grey.point(lambda v: lo + (hi - lo) * (255 - v) // 255)
detail.convert("RGB").save(os.path.join(out, "ice-detail.png"), optimize=True)
print(detail.size, "written")
