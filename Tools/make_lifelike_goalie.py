# Builds the lifelike goalie FBX (Assets/Resources/Models/goalie-dressing.fbx) in Blender, headless.
#
# Run (Git Bash; the .blend is opened with scripts disabled):
#   "/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --disable-autoexec \
#     --python-exit-code 1 --python Tools/make_lifelike_goalie.py -- \
#     --body <Quaternius>/Base Characters/Unity/Superhero_Male_FullBody.fbx --body-textures <Quaternius>/Base Characters/Textures \
#     --gear "<gear>/CP31 V1.blend" --helmet <helmet>/helmet_sketchfab.fbx --helmet-texture <helmet>/Kissel_no_decals.png \
#     --skate <skate>/IceHockey_Skate.fbx --skate-texture <skate>/Hockey_Skate_retopo.jpeg \
#     --out Assets/Resources/Models/goalie-dressing.fbx [--preview <folder for check renders>]
# Blender cannot open paths longer than 260 characters: copy the inputs to a short folder first, and pass Windows
# paths (C:/...), not /c/.... The script writes the FBX and its four goalie-dressing-*.png textures next to it.
#
# What it makes, on the body's own rig (UE-style bones: pelvis, spine_01..03, thigh_l, calf_l, hand_r...), in T-pose:
# - "Skin": the body's head, neck and hands (the rest of the body is under clothes or skates and is removed).
# - "Jersey", "Pants", "Socks": copies of the body's torso/arms, hips/thighs and calves, smoothed, pushed out and given a
#   thickness. They keep the body's skin weights, so they bend with the rig.
# - Rigid gear, each parented to one bone so it follows every joint above it: "Gear Left Pad"/"Gear Right Pad" (calf),
#   "Gear Blocker" and "Gear Stick" (right hand), "Gear Catcher" (left hand), "Gear Mask" and "Gear Visor" (head),
#   "Gear Left Skate"/"Gear Right Skate" (foot).
# Hand frame used for the gear (T-pose): fingers point out along the arm, the back of the hand faces up, the thumb forward.
# The blocker board lies on the back of the right hand, long side along the fingers and forearm; the stick crosses the
# right palm on a diagonal (GRIP_ANGLE), knob back past the heel of the hand, paddle toward the thumb and fingertips, its
# face where the back of the hand faces. The catcher's pocket opens on the palm side of the left hand.
# The jersey hangs straight from the chest to the hem (no waist taper); a dark visor sits inside the mask cage.
# The gear .blend's laces and straps (68 bevelled curves) are dropped: invisible at game distance and many triangles.
import argparse, math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

# ---------------------------------------------------------------- arguments

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
for name in ("body", "body-textures", "gear", "helmet", "helmet-texture", "skate", "skate-texture", "out"):
    ap.add_argument("--" + name, required=True)
ap.add_argument("--preview", default=None)
args = ap.parse_args(argv)

# ---------------------------------------------------------------- look

def srgb_to_linear(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

COLOURS = {  # sRGB
    "Jersey Blue": (0.10, 0.27, 0.66), "Jersey White": (0.93, 0.93, 0.92), "Pants Navy": (0.06, 0.08, 0.18),
    "Sock Blue": (0.10, 0.27, 0.66), "Sock White": (0.93, 0.93, 0.92),
    "Gear White": (0.92, 0.92, 0.90), "Gear Blue": (0.10, 0.27, 0.66), "Gear Grey": (0.55, 0.56, 0.58),
    "Gear Dark": (0.07, 0.07, 0.08), "Stick Black": (0.06, 0.06, 0.07), "Stick Tape": (0.90, 0.90, 0.88),
    "Cage Steel": (0.30, 0.31, 0.33), "Eyebrows": (0.16, 0.10, 0.06), "Visor": (0.03, 0.035, 0.045),
}
GLOSSY = {"Visor": 0.12}  # roughness; everything else 0.6
_materials = {}

def material(name, texture=None):
    if name in _materials:
        return _materials[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    colour = srgb_to_linear(COLOURS.get(name, (1.0, 1.0, 1.0)))
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Roughness"].default_value = GLOSSY.get(name, 0.6)
    m.diffuse_color = (*colour, 1.0)
    if texture is not None:
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = texture
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
        m.diffuse_color = (1.0, 1.0, 1.0, 1.0)
    _materials[name] = m
    return m

TEXDIR = os.path.dirname(os.path.abspath(args.out))  # textures sit next to the FBX, which names them

def load_texture(path, name, size):
    """Loads an image, scales it down to at most size px, saves it as name.png next to the FBX."""
    img = bpy.data.images.load(path)
    w, h = img.size
    if max(w, h) > size:
        k = size / max(w, h)
        img.scale(max(1, int(w * k)), max(1, int(h * k)))
    out = os.path.join(TEXDIR, name + ".png")
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    img.name = name
    return img

# ---------------------------------------------------------------- helpers

scene = bpy.context.scene

def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)

def link(obj):
    scene.collection.objects.link(obj)
    return obj

def baked_copy(obj, name):
    """A new object with obj's evaluated mesh (modifiers applied) in world space, no parent."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    me.transform(obj.matrix_world)
    return link(bpy.data.objects.new(name, me))

def bounds(obj):
    vs = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)])
    hi = Vector([max(v[i] for v in vs) for i in range(3)])
    return lo, hi

def transform(obj, m):
    obj.data.transform(m)
    obj.data.update()

def decimate(obj, target_tris):
    have = tris(obj)
    if have <= target_tris:
        return
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = target_tris / have
    mod.use_collapse_triangulate = True
    apply_modifiers(obj)

def apply_modifiers(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    obj.modifiers.clear()
    old = obj.data
    obj.data = me
    bpy.data.meshes.remove(old)

def colour_by_area(obj, names):
    """Replaces obj's materials: the slot with the most area gets names[0], the next names[1], and so on (last repeats)."""
    me = obj.data
    area = {}
    for p in me.polygons:
        area[p.material_index] = area.get(p.material_index, 0.0) + p.area
    order = sorted(area, key=lambda i: -area[i])
    rank = {slot: min(k, len(names) - 1) for k, slot in enumerate(order)}
    slots = [p.material_index for p in me.polygons]  # clearing the materials resets the indices
    me.materials.clear()
    for n in names:
        me.materials.append(material(n))
    for p, slot in zip(me.polygons, slots):
        p.material_index = rank.get(slot, 0)

def rotation(columns):
    """3x3 rotation whose columns are where the source X, Y, Z axes go."""
    m = Matrix((columns[0], columns[1], columns[2])).transposed()
    return m.to_4x4()

def place(obj, rot, size_axis, size, anchor_from, anchor_to):
    """Rotates obj's mesh by rot about its bounding-box centre, scales it uniformly so its extent along world axis
    size_axis (0/1/2) is size metres, then moves it so the point anchor_from(lo, hi) of its new bounds sits at anchor_to."""
    lo, hi = bounds(obj)
    transform(obj, Matrix.Translation(-(lo + hi) / 2))
    transform(obj, rot)
    lo, hi = bounds(obj)
    transform(obj, Matrix.Scale(size / (hi[size_axis] - lo[size_axis]), 4))
    lo, hi = bounds(obj)
    transform(obj, Matrix.Translation(anchor_to - anchor_from(lo, hi)))

def parent_to_bone(obj, arm, bone):
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    bpy.context.view_layer.update()
    obj.matrix_world = world

# ---------------------------------------------------------------- body

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=args.body)
arm = next(o for o in scene.objects if o.type == "ARMATURE")
arm.data.pose_position = "REST"
body = next(o for o in scene.objects if o.type == "MESH" and o.name.startswith("SuperHero"))
eyes = next((o for o in scene.objects if o.type == "MESH" and o.name.startswith("Eyes")), None)
brows = next((o for o in scene.objects if o.type == "MESH" and o.name.startswith("Eyebrows")), None)
bpy.context.view_layer.update()  # the rig and meshes are turned 180 degrees about Z in the file; worked in world space below

def bone_pos(name, tail=False):
    b = arm.data.bones[name]
    return arm.matrix_world @ (b.tail_local if tail else b.head_local)

texdir = args.body_textures
skin_tex = load_texture(os.path.join(texdir, "T_Superhero_Male_Ligh.png"), "goalie-dressing-body", 1024)
eye_tex = load_texture(os.path.join(texdir, "T_Eye_Brown.png"), "goalie-dressing-eyes", 256)

GROUPS = {
    "head": ("Head", "neck_01"),
    "hand": ("hand_", "index_", "middle_", "ring_", "pinky_", "thumb_"),
    "foot": ("foot_", "ball_"),
    "calf": ("calf_",),
    "thigh": ("thigh_",),
    "pelvis": ("pelvis",),
    "torso": ("spine_", "clavicle_"),
    "arm": ("upperarm_", "lowerarm_"),
}

def region_weights(obj):
    """Per vertex: {region: summed weight}."""
    names = {g.index: g.name for g in obj.vertex_groups}
    region_of = {}
    for i, n in names.items():
        for region, prefixes in GROUPS.items():
            if any(n.startswith(p) for p in prefixes):
                region_of[i] = region
    out = []
    for v in obj.data.vertices:
        w = {}
        for g in v.groups:
            r = region_of.get(g.group)
            if r:
                w[r] = w.get(r, 0.0) + g.weight
        out.append(w)
    return out

weights = region_weights(body)

def face_region(p):
    total = {}
    for vi in p.vertices:
        for r, w in weights[vi].items():
            total[r] = total.get(r, 0.0) + w
    return max(total, key=total.get) if total else "torso"

face_regions = [face_region(p) for p in body.data.polygons]
face_z = [(body.matrix_world @ p.center).z for p in body.data.polygons]

def garment(name, keep, offset_of, thickness, materials, band=None, smooth=8, shape=None):
    """Copy of the body faces where keep(face index) is true, smoothed, pushed out along the normals by offset_of(vertex
    region weights) metres and given thickness. shape(bm, region weights by vertex) can then reshape it before smoothing.
    band(face centre) picks material slot 1 (stripes)."""
    obj = body.copy()
    obj.data = body.data.copy()
    obj.name = obj.data.name = name
    link(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not keep(f.index)], context="FACES")
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    # Taubin smoothing of the pushed-out surface: keeps the size, loses the superhero muscles (a baggy garment).
    deform = bm.verts.layers.deform.active
    region_of = {g.index: g.name for g in obj.vertex_groups}
    target, region_w = {}, {}
    for v in bm.verts:
        w = {}
        for gi, wt in v[deform].items():
            for region, prefixes in GROUPS.items():
                if any(region_of[gi].startswith(p) for p in prefixes):
                    w[region] = w.get(region, 0.0) + wt
        target[v] = v.co + v.normal * offset_of(w, v.co)
        region_w[v] = w
    for v in bm.verts:
        v.co = target[v]
    if shape:
        shape(bm, region_w)
    boundary = {v for v in bm.verts if v.is_boundary}
    for _ in range(smooth):
        for k in (0.5, -0.53):
            new = {}
            for v in bm.verts:
                if v in boundary or not v.link_edges:
                    continue
                avg = sum((e.other_vert(v).co for e in v.link_edges), Vector()) / len(v.link_edges)
                new[v] = v.co + (avg - v.co) * k
            for v, co in new.items():
                v.co = co
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.materials.clear()
    for m in materials:
        obj.data.materials.append(material(m))
    for p in obj.data.polygons:
        p.material_index = 1 if band and band(obj.matrix_world @ p.center) else 0
    mod = obj.modifiers.new("Thickness", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = -1.0
    mod.use_rim = True
    mod.use_even_offset = False
    # Bake the thickness in (the rig is in its rest pose, so the Armature modifier changes nothing); keep the Armature
    # modifier and the vertex groups so the garment still bends with the rig.
    for m in obj.modifiers:
        if m.type == "ARMATURE":
            m.show_viewport = False
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    obj.modifiers.remove(obj.modifiers["Thickness"])
    for m in obj.modifiers:
        m.show_viewport = True
    old = obj.data
    obj.data = me
    bpy.data.meshes.remove(old)
    return obj

def mix(w, table):
    total = sum(w.get(r, 0.0) for r in table)
    return sum(w.get(r, 0.0) * table[r] for r in table) / total if total > 0 else max(table.values())

HEM_Z = 0.86      # jersey hem: over the top of the pants
PANTS_BOTTOM = 0.57  # pants end just above the knee (calf bone head at about 0.54)

def hang_straight(bm, region_w):
    """A goalie jersey hangs straight down from the chest: no waist taper, roughly as wide at the hem as under the arms.
    Pushes the torso (not the sleeves or thighs) out to an upright elliptic tube as wide and deep as the chest under the
    arms (or the hips, if wider), fading out over the shoulders."""
    chest_z = bone_pos("spine_03").z
    def torso(v):
        w = region_w[v]
        return w.get("arm", 0.0) < 0.2 and w.get("thigh", 0.0) < 0.3 and w.get("head", 0.0) < 0.1 and abs(v.co.x) < 0.28
    def half_extents(z):
        s = [v.co for v in bm.verts if abs(v.co.z - z) < 0.015 and torso(v)]
        return max(abs(c.x) for c in s), (max(c.y for c in s) - min(c.y for c in s)) / 2, (max(c.y for c in s) + min(c.y for c in s)) / 2
    ca, cb, cy = half_extents(chest_z)
    ha, hb, _ = half_extents(HEM_Z + 0.06)
    a, b = max(ca, ha) + 0.01, max(cb, hb) + 0.01
    top = chest_z + 0.06
    for v in bm.verts:
        z = v.co.z
        if not torso(v) or z < HEM_Z + 0.02 or z > top:
            continue
        k = 1.0 if z <= chest_z else (top - z) / (top - chest_z)
        dx, dy = v.co.x, v.co.y - cy
        r = math.sqrt((dx / a) ** 2 + (dy / b) ** 2)
        if r <= 1e-6 or r >= 1.0:
            continue
        grow = 1.0 + (1.0 / r - 1.0) * k
        v.co.x, v.co.y = dx * grow, cy + dy * grow

jersey = garment(
    "Jersey",
    lambda i: face_regions[i] in ("torso", "arm") or (face_regions[i] in ("pelvis", "thigh") and face_z[i] > HEM_Z),
    lambda w, co: mix(w, {"torso": 0.06, "arm": 0.04, "pelvis": 0.07, "thigh": 0.07, "head": 0.03, "hand": 0.03}),
    0.012, ["Jersey Blue", "Jersey White"],
    band=lambda c: (HEM_Z + 0.02 < c.z < HEM_Z + 0.09 and abs(c.x) < 0.3) or (0.50 < abs(c.x) < 0.57 and c.z > 1.3),
    smooth=20, shape=hang_straight,
)
pants = garment(
    "Pants",
    lambda i: face_regions[i] in ("pelvis", "thigh") or (face_regions[i] == "calf" and face_z[i] > PANTS_BOTTOM),
    lambda w, co: mix(w, {"pelvis": 0.065, "thigh": 0.05, "calf": 0.03, "torso": 0.06, "foot": 0.03}),
    0.012, ["Pants Navy"],
)
socks = garment(
    "Socks",
    lambda i: face_regions[i] == "calf" and face_z[i] <= PANTS_BOTTOM + 0.04,
    lambda w, co: 0.012,
    0.006, ["Sock Blue", "Sock White"],
    band=lambda c: 0.30 < c.z < 0.36, smooth=3,
)

# Head size, for the mask.
head_pts = [body.matrix_world @ v.co for v in body.data.vertices if weights[v.index].get("head", 0.0) > 0.5 and v.co.z > bone_pos("Head").z - 0.02]
head_lo = Vector([min(p[i] for p in head_pts) for i in range(3)])
head_hi = Vector([max(p[i] for p in head_pts) for i in range(3)])

# The body keeps only what shows: head, neck and hands.
bm = bmesh.new()
bm.from_mesh(body.data)
bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[f for f in bm.faces if face_regions[f.index] not in ("head", "hand")], context="FACES")
bm.to_mesh(body.data)
bm.free()
body.data.materials.clear()
body.data.materials.append(material("Skin", skin_tex))
for part, mat in ((eyes, material("Eyes", eye_tex)), (brows, material("Eyebrows"))):
    if part is not None:
        part.data.materials.clear()
        part.data.materials.append(mat)
body.name = body.data.name = "Skin"
if eyes is not None:
    eyes.name = "Eyes"
if brows is not None:
    brows.name = "Eyebrows"

# ---------------------------------------------------------------- gear .blend (meshes only; nothing in it is run)

with bpy.data.libraries.load(args.gear, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n in ("Cube", "Cube.001", "Cube.002", "Cube.003")]
gear = {}
for o in dst.objects:
    link(o)
bpy.context.view_layer.update()
for o in list(dst.objects):
    gear[o.name] = baked_copy(o, o.name + " baked")
    bpy.data.objects.remove(o)

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
centre = lambda lo, hi: (lo + hi) / 2

# Leg pads: in the .blend they stand in a mirrored pair along Y, faces toward -X. Turn them to face +Y (the body's front).
pads = gear["Cube"]
transform(pads, Matrix.Translation(-centre(*bounds(pads))))
transform(pads, rotation((-Y, X, Z)))  # pad faces (-X in the file) -> +Y, the straps (+X) -> -Y
bm = bmesh.new()
bm.from_mesh(pads.data)
left_faces = [f for f in bm.faces if f.calc_center_median().x < 0]
bm.free()
pad_pair = []
for side, keep_neg in (("Left", True), ("Right", False)):
    o = pads.copy()
    o.data = pads.data.copy()
    o.name = "Gear %s Pad" % side
    link(o)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if (f.calc_center_median().x < 0) != keep_neg], context="FACES")
    bm.to_mesh(o.data)
    bm.free()
    pad_pair.append((side, o))
bpy.data.objects.remove(pads)
PAD_HEIGHT, PAD_WIDTH = 0.86, 0.26
for side, o in pad_pair:
    s = "l" if side == "Left" else "r"
    calf, foot = bone_pos("calf_" + s), bone_pos("foot_" + s)
    decimate(o, 7500)
    colour_by_area(o, ["Gear White", "Gear Blue", "Gear Grey"])
    lo, hi = bounds(o)
    transform(o, Matrix.Translation(-centre(lo, hi)))
    transform(o, Matrix.Diagonal((PAD_WIDTH / (hi.x - lo.x), PAD_HEIGHT / (hi.z - lo.z), PAD_HEIGHT / (hi.z - lo.z), 1.0)))
    lo, hi = bounds(o)
    outward = -1.0 if s == "l" else 1.0
    # Bottom just off the ice over the skate, back against the shin, a little outside the leg so the pair meet in the middle.
    transform(o, Matrix.Translation(Vector((calf.x + outward * 0.02, calf.y - 0.06, 0.03)) - Vector((centre(lo, hi).x, lo.y, lo.z))))
    parent_to_bone(o, arm, "calf_" + s)

# Blocker on the right hand. In the .blend the board faces -X, long side along Z. Board -> back of hand (+Z),
# long side -> along the fingers and forearm (X), so the glove part behind the board (+X) is on the palm side (-Z).
# The board reaches from past the fingertips back over the wrist.
hand_r, hand_l = bone_pos("hand_r"), bone_pos("hand_l")
palm_r = (hand_r + bone_pos("middle_01_r")) / 2
palm_l = (hand_l + bone_pos("middle_01_l")) / 2
blocker = gear["Cube.001"]
blocker.name = "Gear Blocker"
decimate(blocker, 5000)
colour_by_area(blocker, ["Gear White", "Gear Blue", "Gear Dark", "Gear Grey"])
place(blocker, rotation((-Z, Y, X)), 0, 0.40, lambda lo, hi: Vector((centre(lo, hi).x, centre(lo, hi).y, hi.z)),
      palm_r + Vector((-0.04, 0.0, 0.07)))
# The board is bent along its length (one end rises about 35 degrees); turn it about the hand's thumb line so that, on
# average, its face lies flat over the back of the hand and the forearm.
up_faces = [p for p in blocker.data.polygons if p.normal.z > 0.5]
board_up = sum((p.normal * p.area for p in up_faces), Vector()).normalized()
lo, hi = bounds(blocker)
transform(blocker, Matrix.Translation(centre(lo, hi)) @ board_up.rotation_difference(Z).to_matrix().to_4x4()
          @ Matrix.Translation(-centre(lo, hi)))
lo, hi = bounds(blocker)
transform(blocker, Matrix.Translation(Vector((palm_r.x - 0.04 - centre(lo, hi).x, 0.0, 0.0))))
def flat_z(o, sign):
    """Area-weighted height of o's faces that face straight up (sign 1) or down (sign -1)."""
    ps = [p for p in o.data.polygons if p.normal.z * sign > 0.9]
    return sum((o.matrix_world @ p.center).z * p.area for p in ps) / sum(p.area for p in ps)
# The board is the big flat part: its underside rests just over the back of the hand.
transform(blocker, Matrix.Translation(Vector((0.0, 0.0, palm_r.z + 0.03 - flat_z(blocker, -1)))))
parent_to_bone(blocker, arm, "hand_r")

# Catcher on the left hand: in the .blend the back of the hand faces +X, the fingers +Z.
catcher = gear["Cube.003"]
catcher.name = "Gear Catcher"
decimate(catcher, 4500)
colour_by_area(catcher, ["Gear White", "Gear Blue", "Gear Dark", "Gear Grey"])
place(catcher, rotation((Z, Y, -X)), 0, 0.42, centre, palm_l + Vector((-0.03, 0.0, 0.0)))
parent_to_bone(catcher, arm, "hand_l")

# Stick through the right fist, across the palm on a diagonal (as with the index finger down the paddle): the shaft runs
# GRIP_ANGLE from the thumb line toward the fingertips, heel toward the thumb and fingertips (+X+Y), knob back past the
# heel of the hand. The blade points square to the shaft toward the thumb side, its face where the back of the hand faces
# (+Z). With the board (along the fingers) upright and facing the shooter, the paddle then slants down and inward.
stick = gear["Cube.002"]
stick.name = "Gear Stick"
colour_by_area(stick, ["Stick Black", "Stick Tape"])
vs = [v.co.copy() for v in stick.data.vertices]
zmin = min(v.z for v in vs)
low = [v for v in vs if v.z < zmin + 0.6]
knob = max(vs, key=lambda v: v.z)
heel = min(low, key=lambda v: v.y)
toe = max(low, key=lambda v: v.y)
axis = (heel - knob).normalized()                       # knob -> heel
blade = (toe - heel)
blade = (blade - axis * blade.dot(axis)).normalized()   # along the blade, square to the shaft
face = axis.cross(blade).normalized()
src_frame = Matrix((axis, blade, face)).transposed().to_4x4()   # columns: shaft, blade, face in .blend space
GRIP_ANGLE = math.radians(60)
shaft_dir = X * math.sin(GRIP_ANGLE) + Y * math.cos(GRIP_ANGLE)
blade_dir = -X * math.cos(GRIP_ANGLE) + Y * math.sin(GRIP_ANGLE)
dst_frame = Matrix((shaft_dir, blade_dir, Z)).transposed().to_4x4()
STICK_LENGTH, GRIP_FROM_HEEL = 1.5, 0.62
k = STICK_LENGTH / (heel - knob).length
grip_src = heel - axis * (GRIP_FROM_HEEL / k)
transform(stick, Matrix.Translation(-grip_src))
transform(stick, dst_frame @ src_frame.inverted())
transform(stick, Matrix.Scale(k, 4))
transform(stick, Matrix.Translation(palm_r + Vector((0.0, 0.0, -0.01))))
parent_to_bone(stick, arm, "hand_r")

# Mask: helmet + cage, facing -Y in its file. Turn to face +Y and fit over the head.
before = set(scene.objects)
bpy.ops.import_scene.fbx(filepath=args.helmet)
parts = [o for o in set(scene.objects) - before if o.type == "MESH"]
mask_tex = load_texture(args.helmet_texture, "goalie-dressing-mask", 1024)
for o in parts:
    transform(o, o.matrix_world)
    o.matrix_world = Matrix.Identity(4)
    if o.name.startswith("Cage"):
        o.data.materials.clear()
        o.data.materials.append(material("Cage Steel"))
    else:
        o.data.materials.clear()
        o.data.materials.append(material("Mask", mask_tex))
for o in set(scene.objects) - before:
    if o.type != "MESH":
        bpy.data.objects.remove(o)
helmet = next(o for o in parts if not o.name.startswith("Cage"))
for o in parts:
    if o is not helmet:
        o.select_set(True)
helmet.select_set(True)
bpy.context.view_layer.objects.active = helmet
bpy.ops.object.join()
mask = helmet
mask.name = "Gear Mask"
lo, hi = bounds(mask)
head_w = head_hi.x - head_lo.x
place(mask, rotation((-X, -Y, Z)), 0, head_w + 0.085,
      lambda lo, hi: Vector((centre(lo, hi).x, lo.y, hi.z)),
      Vector(((head_lo.x + head_hi.x) / 2, head_lo.y - 0.035, head_hi.z + 0.03)))
parent_to_bone(mask, arm, "Head")

# Visor: a dark, glossy shell inside the cage, between the face and the bars, from the brow to under the chin and back
# past the cheeks, so the face does not show. Part of an ellipsoid round the head, its front just off the nose.
cage_pts = [mask.matrix_world @ mask.data.vertices[i].co for p in mask.data.polygons
            if mask.data.materials[p.material_index].name == "Cage Steel" for i in p.vertices]
cage_lo_z, cage_hi_z = min(p.z for p in cage_pts), max(p.z for p in cage_pts)
nose = max(head_pts, key=lambda p: p.y)
vc = Vector(((head_lo.x + head_hi.x) / 2, (head_lo.y + head_hi.y) / 2, nose.z))
vr = Vector(((head_hi.x - head_lo.x) / 2 + 0.016, nose.y + 0.006 - vc.y, 0.11))
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=24, radius=1.0)
for v in bm.verts:
    v.co = Vector((vc.x + v.co.x * vr.x, vc.y + v.co.y * vr.y, vc.z + v.co.z * vr.z))
bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().y < vc.y + 0.25 * vr.y
                           or not cage_lo_z - 0.012 < f.calc_center_median().z < cage_hi_z + 0.02], context="FACES")
visor = link(bpy.data.objects.new("Gear Visor", bpy.data.meshes.new("Gear Visor")))
bm.to_mesh(visor.data)
bm.free()
mod = visor.modifiers.new("Thickness", "SOLIDIFY")
mod.thickness = 0.003
mod.offset = -1.0
apply_modifiers(visor)
visor.data.materials.append(material("Visor"))
parent_to_bone(visor, arm, "Head")

# Skates: toe toward -Y in the file. Turn to +Y, fit the foot, blade 4 cm under the sole.
skate_tex = load_texture(args.skate_texture, "goalie-dressing-skate", 1024)
before = set(scene.objects)
bpy.ops.import_scene.fbx(filepath=args.skate)
src = next(o for o in set(scene.objects) - before if o.type == "MESH")
transform(src, src.matrix_world)
src.matrix_world = Matrix.Identity(4)
decimate(src, 2600)
src.data.materials.clear()
src.data.materials.append(material("Skate", skate_tex))
for s, side in (("l", "Left"), ("r", "Right")):
    o = src.copy()
    o.data = src.data.copy()
    o.name = "Gear %s Skate" % side
    link(o)
    foot, toe_end = bone_pos("foot_" + s), bone_pos("ball_leaf_" + s)
    heel_y = foot.y - 0.07
    place(o, rotation((-X, -Y, Z)), 1, (toe_end.y + 0.03) - heel_y,
          lambda lo, hi: Vector((centre(lo, hi).x, lo.y, lo.z)), Vector((foot.x, heel_y, -0.04)))
    parent_to_bone(o, arm, "foot_" + s)
bpy.data.objects.remove(src)

# Everything stands on z = 0 (the blade bottoms): move the rig up by the skate height.
lift = 0.04
arm.location.z += lift
bpy.context.view_layer.update()

# ---------------------------------------------------------------- report and export

total = 0
for o in sorted(scene.objects, key=lambda o: o.name):
    if o.type == "MESH":
        dg = bpy.context.evaluated_depsgraph_get()
        me = o.evaluated_get(dg).to_mesh()
        t = sum(len(p.vertices) - 2 for p in me.polygons)
        o.evaluated_get(dg).to_mesh_clear()
        total += t
        print("GOALIE %-18s %6d tris  parent=%s/%s  mats=%s" % (o.name, t, o.parent.name if o.parent else "-", o.parent_bone or "-",
                                                            [m.name for m in o.data.materials]))
print("GOALIE total %d tris" % total)

if args.preview:
    os.makedirs(args.preview, exist_ok=True)
    blend_path = os.path.join(args.preview, "goalie-build.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, copy=True)

out = os.path.abspath(args.out)
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=False, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
    use_armature_deform_only=False, bake_anim=False, path_mode="STRIP", embed_textures=False,
    mesh_smooth_type="FACE", use_mesh_modifiers=True)
print("GOALIE wrote", out)
