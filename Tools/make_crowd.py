# Builds one crowd fan body at one level of detail, with its body types, hair styles, clips and (far level) card
# pictures, for the 3D crowd (Assets/Resources/Crowd/<name>.bytes), in Blender, headless.
#
# Run (Git Bash; the FBX files are only imported, never run), once per body and level (Tools/make_crowd.sh runs all four):
#   "/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --disable-autoexec \
#     --python-exit-code 1 --python Tools/make_crowd.py -- \
#     --body "<Quaternius>/Base Characters/Unity/Superhero_Male_FullBody.fbx" \
#     --hair "<Quaternius>/Hairstyles/Origin at 0/FBX (Unity)" --hairs Hair_Buzzed,Hair_SimpleParted,Hair_Beard \
#     --out Assets/Resources/Crowd/fan-male-near.bytes --triangles 1500 [--card] [--preview <folder for check renders>]
# Blender cannot open paths longer than 260 characters: copy the inputs to a short folder first, and pass Windows paths
# (C:/...), not /c/.... The body and hair are Quaternius "Universal Base Characters" FBX files (CC0; see CREDITS.md).
#
# The pipeline:
# 1. Import the body; keep its one body mesh (eyes and eyebrows are dropped: too small to see in the stands).
# 2. Cut it to about --triangles triangles, less the hair's share (collapse decimation keeps the skin weights).
# 3. Colour each vertex by the bone that moves it most: shirt (spine, clavicles, arms), pants (pelvis, legs), shoes
#    (feet), skin (neck, head, hands), scalp (top and back of the head). The game paints each region per fan.
# 4. Add each hair style (the pack's hair meshes, turned to face the body's way, cut hard) weighted to the head bone. Each
#    hair vertex keeps its style number: the game shows one style per fan and folds the others to a point.
# 5. Body types: the same mesh made heavier or slimmer round its bones (BODY_TYPES), so they share one topology.
# 6. Pose the rig seated (thighs forward, shins down, forearms on the thighs). The mesh is stored in this pose.
# 7. Make each clip (CLIPS) by moving bones from the seated pose (stand-cheer stands up first), whole sine cycles for the
#    loops, and bake every frame's vertex positions for each body type: a vertex animation texture (VAT), one texel per
#    vertex per frame. No clip turns the head left or right.
# 8. --card (the far level): draw the body front-on, seated and standing, into two small region pictures: the game's
#    camera-facing cards for far fans.
#
# File (little-endian), read by CrowdBody.cs:
#   "CRWD", u32 version (2), u32 vertexCount, u32 triangleCount, u32 bodyTypeCount, u32 clipCount
#   vertexCount x (f32 x, y, z, nx, ny, nz, u8 region, u8 hair style (0 = body), 2 pad)
#                                         the seated pose, Unity space (y up, faces +z), origin on the floor under the hips
#   triangleCount x 3 u16 indices         Unity's (clockwise) winding
#   clipCount x (u8 nameLength, name (ASCII), u32 frameCount, f32 framesPerSecond, u8 loops)
#   bodyTypeCount x clipCount x frameCount x vertexCount x 4 f16 (x, y, z, 0)   the VAT's rows from y = 0 (RGBAHalf)
#   u32 cardCount (0 or 2: seated, standing), then if any: u32 width, u32 height, f32 widthMetres, f32 heightMetres,
#     cardCount x height x width u8 (0 = empty, else region + 1), rows from the bottom
# Blender (x, y, z) goes to Unity (x, z, y): the body faces +Y in Blender and +z in Unity; that flips the handedness,
# so the triangles are written in reverse order.
import argparse, math, os, struct, sys
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--body", required=True)
ap.add_argument("--hair", required=True, help="folder of the pack's 'Origin at 0' hair FBX files")
ap.add_argument("--hairs", required=True, help="comma-separated hair FBX names, in style order 1, 2, ...")
ap.add_argument("--out", required=True)
ap.add_argument("--triangles", type=int, default=1500)
ap.add_argument("--card", action="store_true")
ap.add_argument("--preview", default=None)
args = ap.parse_args(argv)

SKIN, SHIRT, PANTS, SHOES, SCALP = 0, 1, 2, 3, 4   # regions: CrowdBody.Region in the game, same numbers
SHIRT_BONES = ("spine_", "clavicle_", "upperarm_", "lowerarm_")
PANTS_BONES = ("pelvis", "thigh_", "calf_", "root")
SHOE_BONES = ("foot_", "ball_")
HAIR_SHARE = 0.17                                  # of the triangles, for all hair styles together

# Body types (CrowdLooks.BodyTypes uses the numbers): how much fuller each part is round its bones (1 = as made).
BODY_TYPES = [
    ("regular", {}),
    ("heavy", {"spine_": 1.38, "pelvis": 1.3, "thigh_": 1.2, "upperarm_": 1.15, "neck_": 1.15}),
    ("slim", {"spine_": 0.82, "pelvis": 0.86, "thigh_": 0.8, "upperarm_": 0.76, "lowerarm_": 0.8, "calf_": 0.84}),
]
# Clips (name, frames, frames per second, loops). CrowdClip in the game uses the same order.
CLIPS = [("sit-idle", 48, 12.0, True), ("wave", 24, 12.0, True), ("stand-cheer", 24, 12.0, True),
         ("oh", 18, 12.0, False), ("clap", 24, 24.0, True)]
CARD_W, CARD_H = 24, 64

# ---------------------------------------------------------------- the body and its hair


def load_body(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    body = max(meshes, key=lambda o: len(o.data.vertices))
    for o in meshes:
        if o is not body: bpy.data.objects.remove(o, do_unlink=True)
    return arm, body


def triangle_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def select_only(obj):
    for o in bpy.data.objects: o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def decimate(obj, target):
    """Collapse decimation to about target triangles (skin weights are kept)."""
    select_only(obj)
    tri = obj.modifiers.new("Triangulate", "TRIANGULATE")
    bpy.ops.object.modifier_move_to_index(modifier=tri.name, index=0)
    bpy.ops.object.modifier_apply(modifier=tri.name)
    m = obj.modifiers.new("Decimate", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = min(1.0, target / triangle_count(obj))
    m.use_symmetry, m.symmetry_axis = True, "X"
    bpy.ops.object.modifier_move_to_index(modifier=m.name, index=0)
    bpy.ops.object.modifier_apply(modifier=m.name)


def head_point(arm):
    return arm.matrix_world @ arm.data.bones["Head"].head_local


def regions(obj, head):
    """Each vertex's region, from the bone that weighs most on it (and the top and back of the head for the scalp)."""
    names = {g.index: g.name for g in obj.vertex_groups}
    out = []
    for v in obj.data.vertices:
        bone = names[max(v.groups, key=lambda g: g.weight).group] if v.groups else "Head"
        p = obj.matrix_world @ v.co
        if bone.startswith(SHIRT_BONES): r = SHIRT
        elif bone.startswith(PANTS_BONES): r = PANTS
        elif bone.startswith(SHOE_BONES): r = SHOES
        elif bone == "Head" and (p.z > head.z + 0.135 or (p.z > head.z + 0.02 and p.y < head.y - 0.02)): r = SCALP
        else: r = SKIN
        out.append(r)
    return out


def add_hairs(body, names, folder, target_each):
    """Imports each hair, turns it to face the body's way (+Y: the pack's hair faces -Y), cuts it to about target_each
    triangles, weights it all to the head bone and joins it to the body. Returns the number of the body's vertices."""
    count = len(body.data.vertices)
    for style, name in enumerate(names, start=1):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(folder, name + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        hair = next(o for o in new if o.type == "MESH")
        for o in new:
            if o is not hair: bpy.data.objects.remove(o, do_unlink=True)
        hair.parent = None
        hair.data.transform(hair.matrix_world)
        hair.matrix_world = Matrix.Identity(4)
        hair.data.transform(Matrix.Rotation(math.pi, 4, "Z"))
        decimate(hair, target_each)
        hair.data.transform(body.matrix_world.inverted())
        hair.matrix_world = body.matrix_world.copy()
        hair.vertex_groups.clear()
        g = hair.vertex_groups.new(name="Head")
        g.add(list(range(len(hair.data.vertices))), 1.0, "REPLACE")
        style_layer = hair.data.attributes.new("hair_style", "INT", "POINT")
        for i in range(len(hair.data.vertices)): style_layer.data[i].value = style
        print(f"  hair {style} {name}: {triangle_count(hair)} triangles")
        select_only(body)
        hair.select_set(True)
        bpy.ops.object.join()
    return count


def hair_styles(body):
    layer = body.data.attributes.get("hair_style")
    return [d.value for d in layer.data] if layer else [0] * len(body.data.vertices)

# ---------------------------------------------------------------- body types (fuller or slimmer round the bones)


def nearest_on_segment(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.dot(ab))))
    return a + ab * t


def body_type_positions(arm, body, fullness):
    """The body's rest vertex positions (object space) with each vertex pushed out from (or in to) its main bone by
    that bone's fullness. Hair and head vertices keep their place."""
    names = {g.index: g.name for g in body.vertex_groups}
    to_world, to_local = body.matrix_world, body.matrix_world.inverted()
    out = []
    for v in body.data.vertices:
        co = v.co.copy()
        if v.groups and fullness:
            g = max(v.groups, key=lambda g: g.weight)
            bone = names[g.group]
            k = next((f for prefix, f in fullness.items() if bone.startswith(prefix)), 1.0)
            if k != 1.0 and bone in arm.data.bones:
                b = arm.data.bones[bone]
                a, e = arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local
                p = to_world @ co
                q = nearest_on_segment(p, a, e)
                w = min(1.0, g.weight * 1.4)
                co = to_local @ (q + (p - q) * (1.0 + (k - 1.0) * w))
        out.append(co)
    return out

# ---------------------------------------------------------------- posing (armature-space aims, any bone roll)


def update():
    bpy.context.view_layer.update()


def to_arm(arm, world_dir):
    return (arm.matrix_world.to_3x3().inverted() @ Vector(world_dir)).normalized()


def aim(arm, name, world_dir):
    """Turns bone name about its head so it points along world_dir (its children follow)."""
    pb = arm.pose.bones[name]
    head, tail = pb.head.copy(), pb.tail.copy()
    rot = (tail - head).normalized().rotation_difference(to_arm(arm, world_dir)).to_matrix().to_4x4()
    pb.matrix = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ pb.matrix
    update()


def turn(arm, name, world_axis, degrees):
    """Turns bone name about its head by degrees round world_axis (its children follow)."""
    pb = arm.pose.bones[name]
    head = pb.head.copy()
    rot = Matrix.Rotation(math.radians(degrees), 4, to_arm(arm, world_axis))
    pb.matrix = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ pb.matrix
    update()


def lerp3(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def sit(arm):
    """The seated pose, facing +Y: thighs forward, shins down, upper arms down the sides, forearms on the thighs."""
    for side, sx in (("l", -1), ("r", 1)):
        aim(arm, "thigh_" + side, (sx * 0.06, 1.0, -0.08))
        aim(arm, "calf_" + side, (0.0, 0.18, -1.0))
        aim(arm, "foot_" + side, (0.0, 1.0, -0.45))
        aim(arm, "upperarm_" + side, (sx * 0.22, 0.30, -1.0))
        aim(arm, "lowerarm_" + side, (-sx * 0.20, 1.0, -0.30))
        aim(arm, "hand_" + side, (-sx * 0.10, 1.0, -0.55))
    turn(arm, "spine_01", (1, 0, 0), 4)   # a little slouch forward


def store(arm):
    return {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones}


def restore(arm, basis):
    for pb in arm.pose.bones: pb.matrix_basis = basis[pb.name]
    update()


def sit_idle(arm, t, f):
    """Small moves from the seated pose: breathing, a slow lean and shift, a small nod, arms drifting. No head turns."""
    turn(arm, "spine_01", (1, 0, 0), 1.6 * math.sin(t))                      # lean in and back
    turn(arm, "spine_02", (0, 1, 0), 1.5 * math.sin(t + 1.3))                # shift side to side
    turn(arm, "spine_03", (1, 0, 0), 0.9 * math.sin(3 * t))                  # breathing
    turn(arm, "Head", (1, 0, 0), 2.5 * math.sin(2 * t + 0.7))                # nod a little
    for side, sx in (("l", -1), ("r", 1)):
        turn(arm, "upperarm_" + side, (1, 0, 0), 2.5 * math.sin(t + (1.7 if sx > 0 else 4.2)))


def wave(arm, t, f):
    """Seated, the right hand up and waving side to side (two waves a loop)."""
    turn(arm, "spine_01", (1, 0, 0), -3 + 1.0 * math.sin(t))
    aim(arm, "upperarm_r", (0.55, 0.25, 0.75))
    aim(arm, "lowerarm_r", lerp3((0.55, 0.15, 1.0), (-0.25, 0.15, 1.0), 0.5 + 0.5 * math.sin(2 * t)))
    aim(arm, "hand_r", (0.0, 0.1, 1.0))
    turn(arm, "upperarm_l", (1, 0, 0), 2.0 * math.sin(t))


def stand(arm):
    """Standing up straight from the seated pose (legs straight down)."""
    for side, sx in (("l", -1), ("r", 1)):
        aim(arm, "thigh_" + side, (sx * 0.08, 0.04, -1.0))
        aim(arm, "calf_" + side, (0.0, -0.03, -1.0))
        aim(arm, "foot_" + side, (0.0, 1.0, -0.35))
    turn(arm, "spine_01", (1, 0, 0), -6)


def stand_cheer(arm, t, f):
    """Standing, both arms up in a V and pumping, the body bouncing (two pumps a loop)."""
    stand(arm)
    turn(arm, "spine_02", (1, 0, 0), -3 * math.sin(2 * t))
    for side, sx in (("l", -1), ("r", 1)):
        pump = 0.5 + 0.5 * math.sin(2 * t + (0.0 if sx > 0 else 0.6))
        aim(arm, "upperarm_" + side, lerp3((sx * 0.55, 0.15, 0.85), (sx * 0.4, 0.2, 1.0), pump))
        aim(arm, "lowerarm_" + side, lerp3((sx * 0.1, 0.35, 0.95), (sx * 0.2, 0.05, 1.0), pump))
        aim(arm, "hand_" + side, (sx * 0.1, 0.1, 1.0))
    turn(arm, "Head", (1, 0, 0), -6 - 3 * math.sin(2 * t))


def oh(arm, t, f):
    """Seated: an 'oh!': both hands up to the head and the body rocks back, then all back to the seated pose."""
    frames = CLIPS[3][1]
    e = math.sin(math.pi * f / (frames - 1)) ** 2 if frames > 1 else 0.0   # 0 at both ends: starts and ends seated
    if e <= 0: return
    turn(arm, "spine_01", (1, 0, 0), -9 * e)
    turn(arm, "Head", (1, 0, 0), -8 * e)
    for side, sx in (("l", -1), ("r", 1)):
        up = arm.pose.bones["upperarm_" + side]
        now = (arm.matrix_world.to_3x3() @ (up.tail - up.head)).normalized()
        aim(arm, "upperarm_" + side, lerp3(tuple(now), (sx * 0.75, 0.45, 0.5), e))
        lo = arm.pose.bones["lowerarm_" + side]
        now = (arm.matrix_world.to_3x3() @ (lo.tail - lo.head)).normalized()
        aim(arm, "lowerarm_" + side, lerp3(tuple(now), (-sx * 0.55, -0.2, 0.8), e))


def clap(arm, t, f):
    """Seated, forearms up in front, the hands meeting twice a loop."""
    turn(arm, "spine_01", (1, 0, 0), 2.0 * math.sin(2 * t))
    for side, sx in (("l", -1), ("r", 1)):
        open_ = 0.5 + 0.5 * math.cos(2 * t)                # 1 apart, 0 together
        aim(arm, "upperarm_" + side, (sx * 0.25, 0.55, -0.75))
        aim(arm, "lowerarm_" + side, lerp3((-sx * 0.55, 0.8, 0.45), (-sx * 0.05, 0.85, 0.5), open_))
        aim(arm, "hand_" + side, lerp3((-sx * 0.8, 0.6, 0.2), (-sx * 0.3, 0.9, 0.3), open_))


POSES = {"sit-idle": sit_idle, "wave": wave, "stand-cheer": stand_cheer, "oh": oh, "clap": clap}

# ---------------------------------------------------------------- baking


def unity(p):
    return (p.x, p.z, p.y)


def evaluated(body):
    deps = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(deps)
    me = ev.to_mesh()
    pos = [body.matrix_world @ v.co for v in me.vertices]
    nrm = [(body.matrix_world.to_3x3() @ v.normal).normalized() for v in me.vertices]
    ev.to_mesh_clear()
    return pos, nrm


def raster(points, tris, region, style, show_style, half, height):
    """Region picture (rows from the bottom) of the posed points seen from the front (+Y looks back at -Y)."""
    depth = [[-1e9] * CARD_W for _ in range(CARD_H)]
    img = [[0] * CARD_W for _ in range(CARD_H)]
    for a, b, c in tris:
        if any(style[i] not in (0, show_style) for i in (a, b, c)): continue
        ps = [((points[i].x + half) / (2 * half) * CARD_W, points[i].z / height * CARD_H, points[i].y) for i in (a, b, c)]
        r = region[a] if style[a] == 0 else SCALP
        x0, x1 = max(0, int(min(p[0] for p in ps))), min(CARD_W - 1, int(max(p[0] for p in ps)) + 1)
        y0, y1 = max(0, int(min(p[1] for p in ps))), min(CARD_H - 1, int(max(p[1] for p in ps)) + 1)
        (ax, ay, az), (bx, by, bz), (cx, cy, cz) = ps
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12: continue
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                px, py = x + 0.5, y + 0.5
                w0 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / den
                w1 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / den
                w2 = 1 - w0 - w1
                if w0 < 0 or w1 < 0 or w2 < 0: continue
                d = w0 * az + w1 * bz + w2 * cz
                if d > depth[y][x]:
                    depth[y][x], img[y][x] = d, r + 1
    return img


def main():
    arm, body = load_body(args.body)
    raw = triangle_count(body)
    hairs = [h for h in args.hairs.split(",") if h]
    hair_target = max(12, int(args.triangles * HAIR_SHARE / max(1, len(hairs))))
    decimate(body, args.triangles - hair_target * len(hairs))
    head = head_point(arm)
    body_region = regions(body, head)
    print(f"body {os.path.basename(args.body)}: {raw} -> {triangle_count(body)} triangles")
    n_body = add_hairs(body, hairs, args.hair, hair_target)
    region = body_region + [SCALP] * (len(body.data.vertices) - n_body)
    style = hair_styles(body)
    tris = [tuple(p.vertices) for p in body.data.polygons]
    assert all(len(t) == 3 for t in tris)
    assert len(region) == len(style) == len(body.data.vertices)
    print(f"with hair: {len(tris)} triangles, {len(body.data.vertices)} vertices")

    rest = [v.co.copy() for v in body.data.vertices]
    sit(arm)
    seated = store(arm)
    pos, nrm = evaluated(body)
    hips = arm.matrix_world @ arm.pose.bones["pelvis"].head
    floor = min(p.z for p in pos)
    origin = Vector((0.0, hips.y, floor))  # on the floor under the hips (x: the body's middle)

    rows, cards_src = [], {}
    for type_name, fullness in BODY_TYPES:
        for i, co in enumerate(body_type_positions(arm, body, fullness)): body.data.vertices[i].co = co
        body.data.update()
        for name, frames, fps, loops in CLIPS:
            lift = None
            for f in range(frames):
                restore(arm, seated)
                POSES[name](arm, 2 * math.pi * f / frames, f)
                pts = evaluated(body)[0]
                if name == "stand-cheer":
                    if lift is None:
                        restore(arm, seated); stand(arm)
                        lift = floor - min(p.z for p in evaluated(body)[0])
                        restore(arm, seated); POSES[name](arm, 2 * math.pi * f / frames, f)
                    bounce = 0.05 * max(0.0, math.sin(2 * 2 * math.pi * f / frames))
                    pts = [p + Vector((0, 0, lift + bounce)) for p in pts]
                rows.append([p - origin for p in pts])
                if type_name == "regular" and f == 0 and name in ("sit-idle", "stand-cheer"):
                    cards_src[name] = rows[-1]
            if args.preview and type_name == "regular":
                preview(body, region, style, args.preview, f"{name}")
        if args.preview: preview(body, region, style, args.preview, f"type-{type_name}", restore_seated=(arm, seated))
        for i, co in enumerate(rest): body.data.vertices[i].co = co
        body.data.update()
    restore(arm, seated)

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "wb") as o:
        o.write(b"CRWD" + struct.pack("<IIIII", 2, len(pos), len(tris), len(BODY_TYPES), len(CLIPS)))
        for p, n, r, s in zip(pos, nrm, region, style):
            o.write(struct.pack("<6fBB2x", *unity(p - origin), *unity(n), r, s))
        for a, b, c in tris:
            o.write(struct.pack("<3H", c, b, a))   # reversed: the axis swap flips the handedness
        for name, frames, fps, loops in CLIPS:
            nb = name.encode("ascii")
            o.write(struct.pack("<B", len(nb)) + nb + struct.pack("<IfB", frames, fps, 1 if loops else 0))
        for frame in rows:
            o.write(b"".join(struct.pack("<4e", *unity(p), 0.0) for p in frame))
        if args.card:
            allp = cards_src["sit-idle"] + cards_src["stand-cheer"]
            half = max(abs(p.x) for p in allp) + 0.02
            height = max(p.z for p in allp) + 0.02
            o.write(struct.pack("<IIIff", 2, CARD_W, CARD_H, 2 * half, height))
            for name in ("sit-idle", "stand-cheer"):
                img = raster(cards_src[name], tris, region, style, 1, half, height)
                o.write(bytes(v for row in img for v in row))
        else:
            o.write(struct.pack("<I", 0))
    size = os.path.getsize(args.out)
    print(f"{args.out} written: {size} bytes, {len(BODY_TYPES)} body types, clips "
          + ", ".join(f"{c[0]} {c[1]}f" for c in CLIPS))


def preview(body, region, style, folder, label, restore_seated=None):
    """A check render of the posed body coloured by region (front and side); all hair styles show at once."""
    os.makedirs(folder, exist_ok=True)
    colours = {SKIN: (0.8, 0.6, 0.5, 1), SHIRT: (0.5, 0.1, 0.15, 1), PANTS: (0.15, 0.15, 0.2, 1), SHOES: (0.05, 0.05, 0.05, 1), SCALP: (0.3, 0.2, 0.1, 1)}
    me = body.data
    layer = me.color_attributes.get("Region") or me.color_attributes.new("Region", "BYTE_COLOR", "POINT")
    for i, r in enumerate(region): layer.data[i].color = colours[r] if style[i] in (0, 1) else (0.6, 0.6, 0.1, 1)
    me.color_attributes.active_color = layer
    if restore_seated: restore(*restore_seated)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "VERTEX"
    scene.render.resolution_x, scene.render.resolution_y = 400, 400
    cam = scene.camera
    if cam is None:
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
        scene.collection.objects.link(cam)
        scene.camera = cam
    for name, loc in (("front", (0.6, 3.4, 1.0)), ("side", (3.4, 0.4, 1.0))):
        cam.location = loc
        cam.rotation_euler = (Vector((0, 0.2, 0.85)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(folder, f"{os.path.splitext(os.path.basename(args.out))[0]}-{label}-{name}.png")
        bpy.ops.render.render(write_still=True)


main()
