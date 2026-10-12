# Builds one crowd fan body and its clips for the 3D crowd (Assets/Resources/Crowd/<name>.bytes) in Blender, headless.
#
# Run (Git Bash; the FBX is only imported, never run):
#   "/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --disable-autoexec \
#     --python-exit-code 1 --python Tools/make_crowd.py -- \
#     --body "<Quaternius>/Base Characters/Unity/Superhero_Male_FullBody.fbx" \
#     --out Assets/Resources/Crowd/fan-superhero-male.bytes [--triangles 1500] [--preview <folder for check renders>]
# Blender cannot open paths longer than 260 characters: copy the input to a short folder first, and pass Windows paths
# (C:/...), not /c/.... The body is a Quaternius "Universal Base Characters" FBX (CC0; see CREDITS.md).
#
# The pipeline (repeat it for each body; #95 adds bodies and clips):
# 1. Import the body; keep its one body mesh (eyes and eyebrows are dropped: too small to see in the stands).
# 2. Cut it to about --triangles triangles (collapse decimation keeps the skin weights).
# 3. Colour each vertex by the bone that moves it most: shirt (spine, clavicles, arms), pants (pelvis, legs), shoes
#    (feet), skin (neck, head, hands), hair (top and back of the head). The game paints each region per fan.
# 4. Pose the rig seated (thighs forward, shins down, forearms on the thighs). The mesh is stored in this pose.
# 5. Make each clip by moving bones from the seated pose, one exact loop of whole sine cycles, and bake every frame's
#    vertex positions: a vertex animation texture (VAT), one texel per vertex per frame.
#
# Clips: "sit-idle" (small breathing, a slow lean and a look round). The Universal Base Characters Standard pack has no
# clips, so they are made here.
#
# File (little-endian), read by CrowdBody.cs:
#   "CRWD", u32 version (1), u32 vertexCount, u32 triangleCount,
#   vertexCount x (f32 x, y, z, nx, ny, nz, u8 region, 3 pad)   the seated pose, Unity space (y up, faces +z),
#                                                               origin on the floor under the hips
#   triangleCount x 3 u16 indices                                Unity's (clockwise) winding
#   u32 clipCount, then per clip: u8 nameLength, name (ASCII), u32 frameCount, f32 framesPerSecond,
#     frameCount x vertexCount x 4 f16 (x, y, z, 0)              the texture's rows from y = 0 (RGBAHalf)
# Blender (x, y, z) goes to Unity (x, z, y): the body faces +Y in Blender and +z in Unity; that flips the handedness,
# so the triangles are written in reverse order.
import argparse, math, os, struct, sys
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--body", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--triangles", type=int, default=1500)
ap.add_argument("--preview", default=None)
args = ap.parse_args(argv)

SKIN, SHIRT, PANTS, SHOES, HAIR = 0, 1, 2, 3, 4   # regions: CrowdBody.Region in the game, same numbers
SHIRT_BONES = ("spine_", "clavicle_", "upperarm_", "lowerarm_")
PANTS_BONES = ("pelvis", "thigh_", "calf_", "root")
SHOE_BONES = ("foot_", "ball_")

SIT_IDLE_FRAMES, SIT_IDLE_FPS = 48, 12.0          # a 4 s loop; the shader blends between frames

# ---------------------------------------------------------------- the body


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


def decimate(obj, target):
    """Collapse decimation to about target triangles, applied under the armature (skin weights are kept)."""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    tri = obj.modifiers.new("Triangulate", "TRIANGULATE")
    bpy.ops.object.modifier_move_to_index(modifier=tri.name, index=0)
    bpy.ops.object.modifier_apply(modifier=tri.name)
    m = obj.modifiers.new("Decimate", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = min(1.0, target / triangle_count(obj))
    m.use_symmetry, m.symmetry_axis = True, "X"
    bpy.ops.object.modifier_move_to_index(modifier=m.name, index=0)
    bpy.ops.object.modifier_apply(modifier=m.name)


def regions(obj):
    """Each vertex's region, from the bone that weighs most on it (and the head's top and back for hair)."""
    names = {g.index: g.name for g in obj.vertex_groups}
    out = []
    for v in obj.data.vertices:
        bone = names[max(v.groups, key=lambda g: g.weight).group] if v.groups else "Head"
        p = obj.matrix_world @ v.co
        if bone.startswith(SHIRT_BONES): r = SHIRT
        elif bone.startswith(PANTS_BONES): r = PANTS
        elif bone.startswith(SHOE_BONES): r = SHOES
        elif bone == "Head" and (p.z > 1.735 or (p.z > 1.62 and p.y < 0.0)): r = HAIR   # T-pose heights (1.81 m tall)
        else: r = SKIN
        out.append(r)
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


def sit_idle(arm, t):
    """Small moves from the seated pose at loop angle t (0..2 pi): breathing, a slow lean, a look round."""
    turn(arm, "spine_01", (1, 0, 0), 1.6 * math.sin(t))                      # lean in and back
    turn(arm, "spine_02", (0, 1, 0), 1.5 * math.sin(t + 1.3))                # shift side to side
    turn(arm, "spine_03", (1, 0, 0), 0.9 * math.sin(3 * t))                  # breathing
    turn(arm, "neck_01", (0, 0, 1), 7.0 * math.sin(t + 0.5) + 3.0 * math.sin(2 * t + 2.0))   # look round
    turn(arm, "Head", (1, 0, 0), 3.0 * math.sin(2 * t + 0.7))                # nod a little
    for side, sx in (("l", -1), ("r", 1)):
        turn(arm, "upperarm_" + side, (1, 0, 0), 2.5 * math.sin(t + (1.7 if sx > 0 else 4.2)))

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


def main():
    arm, body = load_body(args.body)
    raw = triangle_count(body)
    decimate(body, args.triangles)
    region = regions(body)
    tris = [tuple(p.vertices) for p in body.data.polygons]
    assert all(len(t) == 3 for t in tris)
    print(f"body {os.path.basename(args.body)}: {raw} -> {len(tris)} triangles, {len(body.data.vertices)} vertices")

    sit(arm)
    seated = store(arm)
    pos, nrm = evaluated(body)
    hips = arm.matrix_world @ arm.pose.bones["pelvis"].head
    floor = min(p.z for p in pos)
    origin = Vector((0.0, hips.y, floor))  # on the floor under the hips (x: the body's middle)

    frames = []
    for f in range(SIT_IDLE_FRAMES):
        restore(arm, seated)
        sit_idle(arm, 2 * math.pi * f / SIT_IDLE_FRAMES)
        frames.append([p - origin for p in evaluated(body)[0]])
    restore(arm, seated)

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "wb") as o:
        o.write(b"CRWD" + struct.pack("<III", 1, len(pos), len(tris)))
        for p, n, r in zip(pos, nrm, region):
            o.write(struct.pack("<6fB3x", *unity(p - origin), *unity(n), r))
        for a, b, c in tris:
            o.write(struct.pack("<3H", c, b, a))   # reversed: the axis swap flips the handedness
        o.write(struct.pack("<I", 1))
        name = b"sit-idle"
        o.write(struct.pack("<B", len(name)) + name + struct.pack("<If", SIT_IDLE_FRAMES, SIT_IDLE_FPS))
        for frame in frames:
            o.write(b"".join(struct.pack("<4e", *unity(p), 0.0) for p in frame))
    size = os.path.getsize(args.out)
    top = max(p.z for p in pos) - floor
    print(f"{args.out} written: {size} bytes, seated height {top:.2f} m, hips {hips.z - floor:.2f} m up, "
          f"clip sit-idle {SIT_IDLE_FRAMES} frames at {SIT_IDLE_FPS} fps")

    if args.preview: preview(body, region, args.preview)


def preview(body, region, folder):
    """Check renders of the seated, decimated body coloured by region (front and side)."""
    os.makedirs(folder, exist_ok=True)
    colours = {SKIN: (0.8, 0.6, 0.5, 1), SHIRT: (0.8, 0.4, 0.1, 1), PANTS: (0.15, 0.15, 0.2, 1), SHOES: (0.05, 0.05, 0.05, 1), HAIR: (0.3, 0.2, 0.1, 1)}
    me = body.data
    layer = me.color_attributes.new("Region", "BYTE_COLOR", "POINT")
    for i, r in enumerate(region): layer.data[i].color = colours[r]
    me.color_attributes.active_color = layer
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "VERTEX"
    scene.render.resolution_x, scene.render.resolution_y = 600, 600
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    for name, loc in (("front", (0.6, 3.4, 0.9)), ("side", (3.4, 0.4, 0.9))):
        cam.location = loc
        cam.rotation_euler = (Vector((0, 0.2, 0.75)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(folder, f"fan-{name}.png")
        bpy.ops.render.render(write_still=True)


main()
