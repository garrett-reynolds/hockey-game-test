# Builds ONLY a new goalie jersey on the goalie's rig: Assets/Resources/Models/goalie-jersey.fbx (Blender, headless).
#
# Run (Git Bash; paths as Windows paths C:/...):
#   "/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --disable-autoexec \
#     --python-exit-code 1 --python Tools/make_goalie_jersey.py -- \
#     --body "<Quaternius>/Superhero_Male_FullBody.fbx" --out Assets/Resources/Models/goalie-jersey.fbx
# Needs only the Quaternius body (the same rig goalie-dressing.fbx was built on). goalie-dressing.fbx is not touched.
#
# The rig is prepared exactly as make_lifelike_goalie.py does (rest pose, lifted 0.04 m so the blades stand on z = 0), so the
# bones and the mesh bind poses match goalie-dressing.fbx. The export keeps the same bone names (all 65) and the same axes.
#
# The jersey is a goalie cut, built from scratch (not copied from the body): a boxy, loose torso (flat chest, straight sides,
# flat shoulders, hem straight at the upper thigh) and two straight wide sleeves, with a few soft vertical folds, solidified
# 7 mm. Skin weights come from the nearest body surface, except the hem, which hangs from the pelvis.
# Real UVs (one 1024 px map, 650 px per metre):
#   torso island: U 0..1 round the body by arc length (front centre U=.25, back .75, seam at the right side), V = (z-0.90)/0.70*0.4375
#   left sleeve:  U 0..0.40, right sleeve U 0.5..0.90; V = 0.5 + d/0.535*0.34, d = metres from the sleeve's inner end (|x|-0.18)
# GoalieClothSurface.cs paints the stripes, collar, crest and number 31 on this layout (the classic pattern).
import argparse, math, os, sys
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--body", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--preview", default=None)
args = ap.parse_args(argv)

HEM_Z = 0.90
SLEEVE_IN = 0.18     # inner end of a sleeve (|x|), inside the torso
SLEEVE_OUT = 0.715   # cuff, at the wrist
N_TORSO = 48
N_SLEEVE = 24
THICKNESS = 0.007

# z, half width a, half depth b, centre y, superellipse exponent (2 = ellipse, higher = boxier)
TORSO = [
    # Goalie cut over a chest protector (option A fabric round): the BACK is straight from the hem up to the shoulder blades
    # (back y -0.250 at the hem to -0.234 at z 1.45), the sides straight (half width 0.262 to 0.252), the front as before.
    # b and cy below are (front - back) / 2 and (front + back) / 2; higher exponents make the walls flatter.
    (0.900, 0.262, 0.2225, -0.0275, 3.8),   # deep at the hem: the crouching pelvis pushes the seat back out of a narrower jersey
    (0.950, 0.262, 0.2200, -0.0280, 3.8),
    (1.050, 0.262, 0.2150, -0.0300, 3.8),
    (1.150, 0.261, 0.2085, -0.0315, 3.8),
    (1.250, 0.259, 0.2020, -0.0360, 3.8),
    (1.350, 0.256, 0.1920, -0.0440, 3.7),
    (1.450, 0.252, 0.1810, -0.0530, 3.6),
    (1.520, 0.246, 0.1635, -0.0515, 3.0),
    (1.565, 0.232, 0.1370, -0.0430, 2.5),
    (1.595, 0.160, 0.112, -0.042, 2.2),
    (1.612, 0.098, 0.092, -0.036, 2.0),   # the neck opening ...
    (1.628, 0.098, 0.092, -0.036, 2.0),   # ... and the 16 mm stand of the collar above it
]
# x from the shoulder end, radius, centre y, centre z of the sleeve
SLEEVE = [(0.180, 0.106), (0.260, 0.106), (0.360, 0.102), (0.460, 0.099), (0.560, 0.096), (0.660, 0.094), (0.715, 0.096)]
SLEEVE_CY, SLEEVE_CZ = -0.068, 1.495


def lerp_table(z, col):
    if z <= TORSO[0][0]:
        return TORSO[0][col]
    for lo, hi in zip(TORSO, TORSO[1:]):
        if z <= hi[0]:
            t = (z - lo[0]) / (hi[0] - lo[0])
            return lo[col] + (hi[col] - lo[col]) * t
    return TORSO[-1][col]


# Extra rings exactly where the print has a horizontal edge (the hem stripes and the yoke). The painted edge then lies on a mesh
# edge loop, not across skewed quads (a quad cut by a texture edge draws a saw-tooth where the rings change shape fast, #220).
PRINT_EDGE_Z = [0.940, 0.975, 1.005, 1.040, 1.578]
TORSO = sorted(TORSO + [(z, lerp_table(z, 1), lerp_table(z, 2), lerp_table(z, 3), lerp_table(z, 4)) for z in PRINT_EDGE_Z])
# The same on the sleeves: a ring at each elbow stripe edge (x = SLEEVE_IN + d).
PRINT_EDGE_X = [0.415, 0.450, 0.476, 0.511]
SLEEVE = sorted(SLEEVE + [(x, next(r0 + (r1 - r0) * (x - x0) / (x1 - x0) for (x0, r0), (x1, r1) in zip(SLEEVE, SLEEVE[1:]) if x0 <= x <= x1)) for x in PRINT_EDGE_X])


def fold(phi, z):
    """Soft vertical folds: strongest at the hem, none above the chest."""
    amp = 0.0075 * max(0.0, min(1.0, (1.42 - z) / 0.5))
    shape = 0.55 * math.sin(5 * phi + 0.6 + 2.5 * math.sin(z * 5.0)) + 0.30 * math.sin(11 * phi + 1.7) + 0.15 * math.sin(3 * phi + 0.2)
    return 1.0 + amp * shape / 0.17


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=args.body)
arm = next(o for o in scene.objects if o.type == "ARMATURE")
arm.data.pose_position = "REST"
body = next(o for o in scene.objects if o.type == "MESH" and o.name.startswith("SuperHero"))
bpy.context.view_layer.update()
arm.location.z += 0.04   # same lift as make_lifelike_goalie.py
bpy.context.view_layer.update()

# ---------------------------------------------------------------- geometry (world space)
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
tag = {}  # vertex -> "torso" | "sleeve"


def superellipse(phi, a, b, n):
    c, s = math.cos(phi), math.sin(phi)
    e = 2.0 / n
    return a * math.copysign(abs(c) ** e, c), b * math.copysign(abs(s) ** e, s)


# torso rings
rings = []
ring_arc = []
for z, a, b, cy, n in TORSO:
    pts = []
    for i in range(N_TORSO):
        phi = 2 * math.pi * i / N_TORSO
        f = fold(phi, z)
        x, y = superellipse(phi, a * f, b * f, n)
        pts.append(bm.verts.new((x, cy + y, z)))
        tag[pts[-1]] = "torso"
    rings.append(pts)
    # arc length fraction per vertex (including the closing segment)
    lens = [0.0]
    for i in range(1, N_TORSO + 1):
        lens.append(lens[-1] + (pts[i % N_TORSO].co - pts[i - 1].co).length)
    ring_arc.append([l / lens[-1] for l in lens])


def tz(z):
    return (z - HEM_Z) / 0.70 * 0.4375


for j in range(len(rings) - 1):
    for i in range(N_TORSO):
        i2 = (i + 1) % N_TORSO
        face = bm.faces.new((rings[j][i], rings[j][i2], rings[j + 1][i2], rings[j + 1][i]))
        for loop, (jj, ii) in zip(face.loops, ((j, i), (j, i + 1), (j + 1, i + 1), (j + 1, i))):
            loop[uv].uv = (ring_arc[jj][ii], tz(TORSO[jj][0]))

# sleeves
for side in (-1, 1):
    srings = []
    for xs, r in SLEEVE:
        pts = []
        for i in range(N_SLEEVE):
            phi = 2 * math.pi * i / N_SLEEVE
            rr = r * (1.0 + 0.035 * math.sin(3 * phi + xs * 9.0) * (xs - 0.15))
            v = bm.verts.new((side * xs, SLEEVE_CY + rr * math.cos(phi), SLEEVE_CZ + rr * math.sin(phi)))
            tag[v] = "sleeve"
            pts.append(v)
        srings.append(pts)
    u0 = 0.0 if side < 0 else 0.5
    for j in range(len(srings) - 1):
        for i in range(N_SLEEVE):
            i2 = (i + 1) % N_SLEEVE
            quad = (srings[j][i], srings[j][i2], srings[j + 1][i2], srings[j + 1][i]) if side > 0 else (srings[j][i], srings[j + 1][i], srings[j + 1][i2], srings[j][i2])
            face = bm.faces.new(quad)
            for loop in face.loops:
                jj = next(k for k in range(len(srings)) if loop.vert in srings[k])
                ii = srings[jj].index(loop.vert)
                # the closing column of the quad wraps to U = 1 of the island
                if i == N_SLEEVE - 1 and ii == 0:
                    ii = N_SLEEVE
                d = SLEEVE[jj][0] - SLEEVE_IN
                loop[uv].uv = (u0 + 0.40 * ii / N_SLEEVE, 0.5 + d / 0.535 * 0.34)

bm.normal_update()
bm.faces.ensure_lookup_table()
bm.verts.ensure_lookup_table()
# normals must point out: check on the torso (the middle of the hem ring's first face) and flip all if needed
f0 = bm.faces[len(rings[0]) // 2]
centre = Vector((0.0, TORSO[0][3], f0.calc_center_median().z))
if (f0.calc_center_median() - centre).dot(f0.normal) < 0:
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
bm.normal_update()
# sleeves: flip individually if inward (the winding differs per side)
bad = []
for f in bm.faces:
    if tag[f.verts[0]] == "sleeve":
        c = f.calc_center_median()
        axis_pt = Vector((c.x, SLEEVE_CY, SLEEVE_CZ))
        if (c - axis_pt).dot(f.normal) < 0:
            bad.append(f)
if bad:
    bmesh.ops.reverse_faces(bm, faces=bad)
# weights by tag, computed below from the body; the mesh is built in the body's local space
inv = body.matrix_world.inverted()
mesh = bpy.data.meshes.new("Jersey")
order = list(bm.verts)
for v in order:
    v.co = inv @ v.co
bm.to_mesh(mesh)
world_pos = [body.matrix_world @ v.co for v in mesh.vertices]
tags = [tag[v] for v in order]
bm.free()

obj = bpy.data.objects.new("Jersey", mesh)
scene.collection.objects.link(obj)
obj.parent = arm
obj.matrix_parent_inverse = body.matrix_parent_inverse.copy()
obj.matrix_basis = body.matrix_basis.copy()
bpy.context.view_layer.update()

# ---------------------------------------------------------------- skin weights from the nearest body surface
body_verts = [body.matrix_world @ v.co for v in body.data.vertices]
polys = [tuple(p.vertices) for p in body.data.polygons]
tree = BVHTree.FromPolygons(body_verts, polys)
gname = {g.index: g.name for g in body.vertex_groups}
pelvis_zs = (1.00, 1.13)   # the hem hangs from the pelvis; blends to the spine-skinned body weights above
result = []
for k, p in enumerate(world_pos):
    loc, nrm, pi, dist = tree.find_nearest(p)
    w = {}
    tot = 0.0
    for vi in polys[pi]:
        wd = 1.0 / (0.0005 + (body_verts[vi] - loc).length)
        for g in body.data.vertices[vi].groups:
            n = gname[g.group]
            w[n] = w.get(n, 0.0) + g.weight * wd
        tot += wd
    w = {n: x / tot for n, x in w.items()}
    if tags[k] == "torso" and p.z < pelvis_zs[1]:
        t = max(0.0, min(1.0, (p.z - pelvis_zs[0]) / (pelvis_zs[1] - pelvis_zs[0])))
        t = t * t * (3 - 2 * t)
        w = {n: x * t for n, x in w.items()}
        w["pelvis"] = w.get("pelvis", 0.0) + (1.0 - t)
    top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
    s = sum(x for _, x in top)
    result.append([(n, x / s) for n, x in top if x > 0.0])
names = sorted({n for r in result for n, _ in r})
for n in names:
    obj.vertex_groups.new(name=n)
for k, r in enumerate(result):
    for n, x in r:
        obj.vertex_groups[n].add([k], x, "REPLACE")

# ---------------------------------------------------------------- thickness (baked in, like the other garments), material
mat = bpy.data.materials.new("Jersey Blue")
mat.diffuse_color = (0.10, 0.27, 0.66, 1.0)
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.10, 0.27, 0.66, 1.0)
mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
mesh.materials.append(mat)
for p in mesh.polygons:
    p.use_smooth = True
mod = obj.modifiers.new("Thickness", "SOLIDIFY")
mod.thickness = THICKNESS
mod.offset = -1.0
mod.use_rim = True
mod.use_even_offset = False
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
baked = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
obj.modifiers.remove(mod)
old = obj.data
obj.data = baked
bpy.data.meshes.remove(old)
baked.name = "Jersey"
for p in baked.polygons:
    p.use_smooth = True
am = obj.modifiers.new("Armature", "ARMATURE")
am.object = arm
bpy.context.view_layer.update()

tris = sum(len(p.vertices) - 2 for p in baked.polygons)
print("JERSEY tris", tris, "verts", len(baked.vertices), "uv", [u.name for u in baked.uv_layers], "groups", len(obj.vertex_groups))
bb = [obj.matrix_world @ v.co for v in baked.vertices]
print("JERSEY bbox", [round(min(c[i] for c in bb), 3) for i in range(3)], [round(max(c[i] for c in bb), 3) for i in range(3)])

if args.preview:
    os.makedirs(args.preview, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(args.preview, "goalie-jersey-build.blend"), copy=True)

out = os.path.abspath(args.out)
os.makedirs(os.path.dirname(out), exist_ok=True)
for o in scene.objects:
    o.select_set(o in (arm, obj))
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
    use_armature_deform_only=False, bake_anim=False, path_mode="STRIP", embed_textures=False,
    mesh_smooth_type="FACE", use_mesh_modifiers=True)
print("JERSEY wrote", out)
