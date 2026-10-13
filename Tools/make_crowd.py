# Builds one crowd fan body (one Quaternius Modular outfit) at one level of detail, with its clips and (far level) card
# pictures, for the 3D crowd (Assets/Resources/Crowd/<name>.bytes), in Blender, headless.
#
# Run (Git Bash; the FBX files are only imported, never run), once per outfit and level (Tools/make_crowd.sh runs all 24):
#   "/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --disable-autoexec \
#     --python-exit-code 1 --python Tools/make_crowd.py -- \
#     --outfit "<short folder>/men/Casual_Hoodie.fbx" \
#     --out Assets/Resources/Crowd/fan-m-casual-hoodie-near.bytes --triangles 1500 [--card] [--preview <folder for check renders>]
# Blender cannot open paths longer than 260 characters: copy the inputs to a short folder first (<folder>/men and
# <folder>/women, because the two packs share file names), and pass Windows paths (C:/...), not /c/.... The outfits are
# Quaternius "Ultimate Modular Men" and "Ultimate Modular Women" Individual Characters FBX files (CC0; see CREDITS.md).
#
# The pipeline:
# 1. Import the outfit. Tag each face with its part (the mesh name suffix: Body, Feet, Head, Legs or Pants), join the meshes,
#    drop the eye, eyebrow and earring faces (too small to see in the stands), and turn the rig to face +Y.
# 2. Paint: each face's (part, material) is a kind (skin, team top, hair or own) in OUTFITS below; its colour is the
#    material's Base Color. Edges between different paints are split, so every vertex has one paint (a palette index).
# 3. Cut the mesh to about --triangles triangles (collapse decimation keeps the skin weights and the paint).
# 4. Pose the rig seated (thighs forward, shins down, forearms on the thighs). The mesh is stored in this pose.
# 5. Make each clip (CLIPS) from the seated pose: the pack's own upper-body animation laid on it as a change from the
#    pack's idle stance (Idle, Wave, Punch_*, HitRecieve*, Sword_Slash), or hand-made (clap). stand-cheer stands up first.
#    The head's left and right turn is taken out of every frame. Bake every frame's vertex positions: a vertex animation
#    texture (VAT), one texel per vertex per frame.
# 6. Check the clips (head yaw, loops, once clips) and stop with an error if one fails.
# 7. --card (the far level): draw the body front-on, seated and standing, into two small paint pictures: the game's
#    camera-facing cards for far fans.
#
# File (little-endian), version 3, read by CrowdBody in the game:
#   "CRWD", u32 version (3), u32 vertexCount, u32 triangleCount, u32 bodyTypeCount, u32 clipCount
#   u32 paletteCount (<= 16), then paletteCount x (u8 kind, u8 r, u8 g, u8 b)
#                                         kind: 0 skin, 1 team top, 2 hair, 3 own; sRGB 0..255 (skin and hair: preview only)
#   vertexCount x (f32 x, y, z, nx, ny, nz, u8 paint (palette index), 3 pad)
#                                         the seated pose, Unity space (y up, faces +z), origin on the floor under the hips
#   triangleCount x 3 u16 indices         Unity's (clockwise) winding
#   clipCount x (u8 nameLength, name (ASCII), u32 frameCount, f32 framesPerSecond, u8 loops)
#   bodyTypeCount x clipCount x frameCount x vertexCount x 4 f16 (x, y, z, 0)   the VAT's rows from y = 0 (RGBAHalf)
#   u32 cardCount (0 or 2: seated, standing), then if any: u32 width, u32 height, f32 widthMetres, f32 heightMetres,
#     cardCount x height x width u8 (0 = empty, else paint + 1), rows from the bottom
# Blender (x, y, z) goes to Unity (x, z, y): the body faces +Y in Blender and +z in Unity; that flips the handedness,
# so the triangles are written in reverse order.
import argparse, math, os, re, struct, sys
import bmesh, bpy
from mathutils import Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--outfit", required=True, help="one outfit FBX, in a folder named men or women")
ap.add_argument("--out", required=True)
ap.add_argument("--triangles", type=int, default=1500)
ap.add_argument("--card", action="store_true")
ap.add_argument("--preview", default=None)
args = ap.parse_args(argv)

SKIN, TOP, HAIR, OWN = 0, 1, 2, 3                  # palette kinds: CrowdBody in the game, same numbers
MAX_PALETTE = 16
PARTS = ("Body", "Feet", "Head", "Legs", "Pants")  # the mesh name suffixes
DROP_MATERIALS = ("Eye", "Eyebrows", "Earrings")
SKIN_MATERIALS = ("Skin", "Skin_Darker")
# Per outfit (folder/file): the team-recolourable top and the hair, as "Part:Material". Everything else is own colours.
OUTFITS = {
    "men/Casual_Hoodie": (["Body:Purple"], ["Head:Hair"]),
    "men/Casual_2": (["Body:LightBrown"], ["Head:Hair"]),
    "men/Suit": (["Body:Suit"], ["Head:Hair"]),                   # the jacket only; Legs:Suit stays own
    "men/Worker": (["Body:Worker_Yellow"], ["Head:Moustache"]),   # Head:Worker_Yellow is the hard hat: own
    "men/Punk": (["Body:Black"], []),                             # dyed hair stays own
    "men/Farmer": (["Body:LightBlue"], []),                       # Head:Beige is the hat: own
    "women/Casual": (["Body:White"], ["Head:Hair_Brown", "Head:Hair_Blond"]),
    "women/Suit": (["Body:Black"], ["Head:Hair_Brown", "Head:Hair_Blond"]),
    "women/Formal": (["Body:LimeGreen", "Legs:LimeGreen"], ["Head:Red", "Head:Brown"]),   # one dress across Body and Legs
    "women/Worker": (["Body:Worker_Vest"], ["Head:DarkBrown"]),
    "women/Punk": (["Body:Pink"], ["Head:Hair_Brown"]),
    "women/Adventurer": (["Body:LightGreen"], ["Head:Hair_Brown"]),
}

# Body types (kept so heavy and slim can come back): how much fuller each bone's part is (1 = as made).
BODY_TYPES = [("regular", {})]
# Clips (name, frames, frames per second, loops). CrowdClip in the game uses the same order.
CLIPS = [("sit-idle", 24, 12.0, True), ("wave", 24, 12.0, True), ("stand-cheer", 12, 12.0, True),
         ("oh", 18, 12.0, False), ("clap", 24, 24.0, True), ("miss", 20, 12.0, False),
         ("stand-cheer-right", 12, 12.0, True), ("oh-back", 18, 12.0, False)]
CARD_W, CARD_H = 24, 64
UPPER = ("Abdomen", "Torso", "Chest", "Neck", "Head", "Shoulder.", "UpperArm.", "LowerArm.", "Wrist.")
HEAD_YAW_LIMIT = 5.0     # degrees from seated
LOOP_GAP_LIMIT = 0.06    # metres: the smallest loop-seam jump allowed (last frame to first); the guard is max(this, 1.25 x the clip's biggest step)
ONCE_GAP_LIMIT = 0.01    # metres: once clips' first and last frame to the seated pose

# ---------------------------------------------------------------- the outfit


def srgb(c):
    return round(255 * (12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055))


def material_name(m):
    return re.sub(r"\.\d+$", "", m.name) if m else ""


def material_colour(m):
    """The material's Principled Base Color (linear RGB)."""
    if m and m.node_tree:
        b = m.node_tree.nodes.get("Principled BSDF")
        if b: return tuple(b.inputs["Base Color"].default_value)[:3]
    return tuple(m.diffuse_color)[:3] if m else (0.5, 0.5, 0.5)


def triangle_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def select_only(obj, *more):
    for o in bpy.data.objects: o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for o in more: o.select_set(True)


def evaluated(body):
    deps = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(deps)
    me = ev.to_mesh()
    pos = [body.matrix_world @ v.co for v in me.vertices]
    nrm = [(body.matrix_world.to_3x3() @ v.normal).normalized() for v in me.vertices]
    ev.to_mesh_clear()
    return pos, nrm


def update():
    bpy.context.view_layer.update()


def load_outfit(path):
    """Imports the outfit; returns (armature, joined mesh, a copy of the armature that plays the pack's actions)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.animation_data_clear()                     # the importer leaves one action on the rig; the pose is ours
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in meshes:                                 # tag each face with its part: Formal's head is "Formad_Head"
        suffix = o.name.rsplit("_", 1)[-1]
        assert suffix in PARTS, f"unknown part {o.name}"
        layer = o.data.attributes.new("part", "INT", "FACE")
        for d in layer.data: d.value = PARTS.index(suffix)
    body = max(meshes, key=lambda o: len(o.data.vertices))
    select_only(body, *meshes)
    bpy.ops.object.join()
    # The pack faces -Y; the pipeline needs +Y. Turn the armature 180 degrees about Z and apply it.
    arm.rotation_euler.rotate_axis("Z", math.pi)
    select_only(arm, body)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    update()
    for n in ("Root", "Body", "Hips"): arm.pose.bones[n].location = (0.0, 0.0, 0.0)
    update()
    left = (arm.matrix_world @ arm.pose.bones["UpperArm.L"].head).x
    assert left < 0, f"UpperArm.L is at world x {left:.3f}; the character's left must be -X"
    zs = [p.z for p in evaluated(body)[0]]
    height = max(zs) - min(zs)
    print(f"  standing height {height:.3f} m")
    if not 1.5 <= height <= 2.0:
        k = 1.75 / height
        arm.scale = (k, k, k)
        select_only(arm, body)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        update()
        zs = [p.z for p in evaluated(body)[0]]
        print(f"  scaled by {k:.3f} to {max(zs) - min(zs):.3f} m")
    ghost = arm.copy()                               # shares the bones; plays the pack's actions (after the applies above)
    ghost.name = "Ghost"
    bpy.context.scene.collection.objects.link(ghost)
    ghost.hide_render = True
    return arm, body, ghost


def build_paint(body, key):
    """Drops the eye faces, gives every face a palette entry from OUTFITS and splits edges between different paints, so
    every vertex has one paint (point attribute 'paint'). Returns the palette: [(kind, (r, g, b) sRGB, linear)]."""
    top, hair = (set(x) for x in OUTFITS[key])
    me = body.data
    mats = [material_name(m) for m in me.materials]
    cols = [material_colour(m) for m in me.materials]
    bm = bmesh.new()
    bm.from_mesh(me)
    part_layer = bm.faces.layers.int.get("part")
    face_layer = bm.faces.layers.int.new("paintf")       # layers first: adding one later invalidates the faces
    vert_layer = bm.verts.layers.int.new("paint")
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if mats[f.material_index] in DROP_MATERIALS], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    found, per_face = set(), []
    for f in bm.faces:
        part, mat = PARTS[f[part_layer]], mats[f.material_index]
        tag = f"{part}:{mat}"
        found.add(tag)
        kind = SKIN if mat in SKIN_MATERIALS else TOP if tag in top else HAIR if tag in hair else OWN
        lin = cols[f.material_index]
        per_face.append((f, (kind, tuple(srgb(c) for c in lin), lin)))
    for tag in sorted((top | hair) - found): raise SystemExit(f"OUTFITS['{key}'] names {tag}, which the outfit does not have")
    palette = sorted({e for _, e in per_face}, key=lambda e: e[:2])
    index = {e[:2]: i for i, e in enumerate(palette)}
    if len(palette) > MAX_PALETTE: raise SystemExit(f"{key}: {len(palette)} palette entries, over {MAX_PALETTE}")
    for f, e in per_face: f[face_layer] = index[e[:2]]
    seams = [e for e in bm.edges if len(e.link_faces) == 2 and e.link_faces[0][face_layer] != e.link_faces[1][face_layer]]
    bmesh.ops.split_edges(bm, edges=seams)
    for f in bm.faces:
        for v in f.verts: v[vert_layer] = f[face_layer]
    bm.faces.layers.int.remove(face_layer)
    bm.to_mesh(me)
    bm.free()
    me.update()
    print(f"  palette {len(palette)}: " + ", ".join(f"{i}:k{e[0]}#{e[1][0]:02x}{e[1][1]:02x}{e[1][2]:02x}" for i, e in enumerate(palette)))
    return palette


def decimate(obj, target):
    """Collapse decimation to about target triangles (skin weights and paint are kept)."""
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

# ---------------------------------------------------------------- body types (fuller or slimmer round the bones)


def nearest_on_segment(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.dot(ab))))
    return a + ab * t


def body_type_positions(arm, body, fullness):
    """The body's rest vertex positions (object space) with each vertex pushed out from (or in to) its main bone by
    that bone's fullness. Vertices keep their place when fullness is empty."""
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


def store(arm):
    return {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones}


def restore(arm, basis):
    for pb in arm.pose.bones: pb.matrix_basis = basis[pb.name]
    update()


SIDES = (("L", -1), ("R", 1))   # the character's left is -X when it faces +Y


def follow_shins(arm):
    """The Foot bones hang from Root, not from the shins, but the shoes are weighted to both: move each Foot with its shin."""
    for side, _ in SIDES:
        shin, foot = arm.pose.bones["LowerLeg." + side], arm.pose.bones["Foot." + side]
        move = shin.matrix @ arm.data.bones[shin.name].matrix_local.inverted()
        foot.matrix = move @ arm.data.bones[foot.name].matrix_local
    update()


def sit(arm):
    """The seated pose, facing +Y: thighs forward, shins down, upper arms down the sides, forearms on the thighs. The
    shoes follow the shins (follow_shins)."""
    for side, sx in SIDES:
        aim(arm, "UpperLeg." + side, (sx * 0.06, 1.0, -0.08))
        aim(arm, "LowerLeg." + side, (0.0, 0.18, -1.0))
        aim(arm, "UpperArm." + side, (sx * 0.22, 0.30, -1.0))
        aim(arm, "LowerArm." + side, (-sx * 0.20, 1.0, -0.30))
        aim(arm, "Wrist." + side, (-sx * 0.10, 1.0, -0.55))
    follow_shins(arm)
    turn(arm, "Abdomen", (1, 0, 0), 4)   # a little slouch forward


def stand(arm):
    """Standing up straight from the seated pose (legs straight down)."""
    for side, sx in SIDES:
        aim(arm, "UpperLeg." + side, (sx * 0.08, 0.04, -1.0))
        aim(arm, "LowerLeg." + side, (0.0, -0.03, -1.0))
    follow_shins(arm)
    turn(arm, "Abdomen", (1, 0, 0), -6)


def clap(arm, t):
    """Seated, forearms up in front, the hands meeting twice a loop."""
    turn(arm, "Abdomen", (1, 0, 0), 2.0 * math.sin(2 * t))
    for side, sx in SIDES:
        open_ = 0.5 + 0.5 * math.cos(2 * t)                # 1 apart, 0 together
        aim(arm, "UpperArm." + side, (sx * 0.25, 0.55, -0.75))
        aim(arm, "LowerArm." + side, lerp3((-sx * 0.55, 0.8, 0.45), (-sx * 0.05, 0.85, 0.5), open_))
        aim(arm, "Wrist." + side, lerp3((-sx * 0.8, 0.6, 0.2), (-sx * 0.3, 0.9, 0.3), open_))

# ---------------------------------------------------------------- the pack's own clips (upper body only)


_basis_cache = {}


def pack_action(name):
    return next(a for a in bpy.data.actions if a.name.split("|")[-1] == name)


def pack_basis(ghost, action, frame):
    """Every UPPER bone's matrix_basis in the pack action at the (fractional) frame, on the armature copy."""
    key = (action, round(frame, 4))
    if key not in _basis_cache:
        ac = pack_action(action)
        ghost.animation_data_create()
        ghost.animation_data.action = ac
        if hasattr(ac, "slots") and len(ac.slots): ghost.animation_data.action_slot = ac.slots[0]
        whole = math.floor(frame)
        bpy.context.scene.frame_set(whole, subframe=frame - whole)
        _basis_cache[key] = {pb.name: pb.matrix_basis.copy() for pb in ghost.pose.bones if pb.name.startswith(UPPER)}
    return _basis_cache[key]


def scaled(delta, weight):
    """The change delta (a matrix) at weight 0..1: move, turn and scale each scaled by weight."""
    if weight >= 1.0: return delta
    t, q, s = delta.decompose()
    return Matrix.LocRotScale(t * weight, Quaternion().slerp(q, weight), Vector((1, 1, 1)).lerp(s, weight))


def pack_upper(arm, ghost, action, u, weight=1.0, ref="Idle"):
    """Lays the pack action's upper-body pose at u (0..1 of [start, end)) on the current (seated) pose: each upper bone's
    change from the reference stance (the first frame of ref, the pack's idle stance) is multiplied onto its
    matrix_basis, scaled by weight. Hips, Root, Body and the legs are never copied."""
    a0, a1 = pack_action(action).frame_range
    now = pack_basis(ghost, action, a0 + u * (a1 - a0))
    base = pack_basis(ghost, ref, pack_action(ref).frame_range[0])
    for name, b in now.items():
        pb = arm.pose.bones[name]
        pb.matrix_basis = pb.matrix_basis @ scaled(base[name].inverted_safe() @ b, weight)
    update()


def once_weight(f, frames):
    return math.sin(math.pi * f / (frames - 1)) ** 2 if frames > 1 else 0.0   # 0 at both ends: starts and ends seated


PACK_CLIPS = {"sit-idle": ("Idle", False), "wave": ("Wave", False), "stand-cheer": ("Punch_Left", False),
              "oh": ("HitRecieve", True), "miss": ("Sword_Slash", True), "stand-cheer-right": ("Punch_Right", False),
              "oh-back": ("HitRecieve_2", True)}


def pose_clip(arm, ghost, name, f, frames):
    """Poses the rig for frame f of clip name, starting from the seated pose."""
    if name == "clap":
        clap(arm, 2 * math.pi * f / frames)
        return
    action, once = PACK_CLIPS[name]
    if name.startswith("stand-cheer"): stand(arm)
    pack_upper(arm, ghost, action, f / frames, once_weight(f, frames) if once else 1.0)


def world_rot(arm, name):
    return (arm.matrix_world.to_3x3() @ arm.pose.bones[name].matrix.to_3x3()).normalized()


def yaw_from(arm, name, seated_rot):
    """The bone's turn about the world up axis, in degrees, from its seated rotation (swing-twist about Z)."""
    q = (world_rot(arm, name) @ seated_rot.inverted()).to_quaternion()
    twist = math.hypot(q.w, q.z)
    return 0.0 if twist < 1e-9 else math.degrees(2 * math.atan2(q.z / twist, q.w / twist))


def strip_head_turn(arm, seated_rots):
    """Takes the Neck and Head turn about the world up axis out of the pose: the head always faces where it did seated."""
    for name in ("Neck", "Head"):
        yaw = yaw_from(arm, name, seated_rots[name])
        if abs(yaw) > 1e-6: turn(arm, name, (0, 0, 1), -yaw)


def head_forward_axis(arm):
    """(local axis index, sign) of the Head bone that points forward (+Y) in the rest pose."""
    m = world_rot(arm, "Head")
    return max(((i, s) for i in range(3) for s in (-1, 1)), key=lambda c: c[1] * m.col[c[0]].y)


def head_yaw(arm, axis):
    v = world_rot(arm, "Head").col[axis[0]] * axis[1]
    return math.degrees(math.atan2(v.x, v.y))

# ---------------------------------------------------------------- baking and cards


def unity(p):
    return (p.x, p.z, p.y)


def raster(points, tris, paint, half, height):
    """Paint picture (rows from the bottom) of the posed points seen from the front (+Y looks back at -Y)."""
    depth = [[-1e9] * CARD_W for _ in range(CARD_H)]
    img = [[0] * CARD_W for _ in range(CARD_H)]
    for a, b, c in tris:
        ps = [((points[i].x + half) / (2 * half) * CARD_W, points[i].z / height * CARD_H, points[i].y) for i in (a, b, c)]
        r = paint[a]
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


def fail(message):
    print("CHECK FAILED: " + message)
    raise SystemExit(1)


def main():
    key = os.path.basename(os.path.dirname(os.path.abspath(args.outfit))) + "/" + os.path.splitext(os.path.basename(args.outfit))[0]
    if key not in OUTFITS: raise SystemExit(f"{key} is not in OUTFITS")
    arm, body, ghost = load_outfit(args.outfit)
    raw = triangle_count(body)
    palette = build_paint(body, key)
    decimate(body, args.triangles)
    paint = [d.value for d in body.data.attributes["paint"].data]
    assert all(0 <= p < len(palette) for p in paint), f"{key}: a vertex paints outside its palette"
    print(f"outfit {key}: {raw} -> {triangle_count(body)} triangles, {len(body.data.vertices)} vertices")
    tris = [tuple(p.vertices) for p in body.data.polygons]
    assert all(len(t) == 3 for t in tris)
    assert len(paint) == len(body.data.vertices)

    rest = [v.co.copy() for v in body.data.vertices]
    fwd = head_forward_axis(arm)
    sit(arm)
    seated = store(arm)
    seated_rots = {n: world_rot(arm, n) for n in ("Neck", "Head")}
    seated_yaw = head_yaw(arm, fwd)
    pos, nrm = evaluated(body)
    hips = arm.matrix_world @ arm.pose.bones["Hips"].head
    floor = min(p.z for p in pos)
    origin = Vector((0.0, hips.y, floor))  # on the floor under the hips (x: the body's middle)
    seated_pts = [p - origin for p in pos]

    rows, cards_src = [], {}
    for type_name, fullness in BODY_TYPES:
        for i, co in enumerate(body_type_positions(arm, body, fullness)): body.data.vertices[i].co = co
        body.data.update()
        for name, frames, fps, loops in CLIPS:
            lift, clip = None, []
            worst_yaw = 0.0
            for f in range(frames):
                restore(arm, seated)
                if name.startswith("stand-cheer") and lift is None:
                    stand(arm)
                    lift = floor - min(p.z for p in evaluated(body)[0])
                    restore(arm, seated)
                pose_clip(arm, ghost, name, f, frames)
                strip_head_turn(arm, seated_rots)
                worst_yaw = max(worst_yaw, abs(head_yaw(arm, fwd) - seated_yaw))
                pts = evaluated(body)[0]
                if name.startswith("stand-cheer"):
                    bounce = 0.05 * max(0.0, math.sin(2 * math.pi * f / frames))
                    pts = [p + Vector((0, 0, lift + bounce)) for p in pts]
                clip.append([p - origin for p in pts])
                if args.preview and type_name == "regular" and f == frames // 2:
                    preview(body, palette, paint, args.preview, name)
            if worst_yaw > HEAD_YAW_LIMIT: fail(f"{name}: the head turns {worst_yaw:.1f} degrees from seated")
            gap = max((a - b).length for a, b in zip(clip[0], clip[-1]))
            step = max(max((a - b).length for a, b in zip(clip[i], clip[i + 1])) for i in range(len(clip) - 1))
            print(f"  {name}: {frames} frames, head turn {worst_yaw:.2f} deg, first-last {gap * 100:.1f} cm, "
                  f"biggest step {step * 100:.1f} cm")
            # The seam is one more step round the loop, so a fast clip's seam is as big as its steps: it only has to be no
            # bigger than the clip's own biggest step (25% slack), or small.
            if loops and gap > max(LOOP_GAP_LIMIT, 1.25 * step): fail(f"{name}: loop jumps {gap * 100:.1f} cm from the last frame to the first")
            if not loops:
                for end in (clip[0], clip[-1]):
                    off = max((a - b).length for a, b in zip(end, seated_pts))
                    if off > ONCE_GAP_LIMIT: fail(f"{name}: a once clip's end is {off * 100:.1f} cm off the seated pose")
            rows.extend(clip)
            if type_name == "regular" and name in ("sit-idle", "stand-cheer"): cards_src[name] = clip[0]
        for i, co in enumerate(rest): body.data.vertices[i].co = co
        body.data.update()
    restore(arm, seated)

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "wb") as o:
        o.write(b"CRWD" + struct.pack("<IIIII", 3, len(pos), len(tris), len(BODY_TYPES), len(CLIPS)))
        o.write(struct.pack("<I", len(palette)))
        for kind, rgb, _ in palette: o.write(struct.pack("<4B", kind, *rgb))
        for p, n, c in zip(pos, nrm, paint):
            o.write(struct.pack("<6fB3x", *unity(p - origin), *unity(n), c))
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
                img = raster(cards_src[name], tris, paint, half, height)
                o.write(bytes(v for row in img for v in row))
        else:
            o.write(struct.pack("<I", 0))
    size = os.path.getsize(args.out)
    print(f"{args.out} written: {size} bytes, {len(tris)} triangles, {len(pos)} vertices, {len(palette)} palette, clips "
          + ", ".join(f"{c[0]} {c[1]}f" for c in CLIPS))


PREVIEW_KINDS = {TOP: (0.9, 0.05, 0.05, 1), HAIR: (0.05, 0.85, 0.9, 1)}   # team top red, hair cyan; the rest in pack colours


def preview(body, palette, paint, folder, label):
    """A check render of the current pose coloured by paint (front and side)."""
    os.makedirs(folder, exist_ok=True)
    me = body.data
    layer = me.color_attributes.get("Paint") or me.color_attributes.new("Paint", "FLOAT_COLOR", "POINT")
    for i, c in enumerate(paint):
        kind, _, lin = palette[c]
        layer.data[i].color = PREVIEW_KINDS.get(kind, (*lin, 1))
    me.color_attributes.active_color = layer
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "VERTEX"
    scene.display.shading.light = "STUDIO"
    scene.render.resolution_x, scene.render.resolution_y = 400, 400
    cam = scene.camera
    if cam is None:
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
        scene.collection.objects.link(cam)
        scene.camera = cam
    for name, loc in (("front", (0.6, 3.4, 1.0)), ("side", (3.4, 0.4, 1.0))):
        cam.location = loc
        cam.rotation_euler = (Vector((0, 0.2, 0.85)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(folder, f"{label}-{name}.png")
        bpy.ops.render.render(write_still=True)


main()
