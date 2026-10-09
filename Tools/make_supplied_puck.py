# Cleans the puck model Garrett supplied (hockey_puck.fbx, source unknown) into Assets/Resources/Models/puck.fbx,
# in Blender, headless.
#
# Run (Git Bash; pass Windows paths, C:/..., not /c/...):
#   "/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --disable-autoexec \
#     --python-exit-code 1 --python Tools/make_supplied_puck.py -- \
#     --in <folder>/hockey_puck.fbx --out Assets/Resources/Models/puck.fbx
#
# What it changes:
# - Drops the embedded 4096 px bump.png (12 MB; it only fed the face's roughness) and the normal-map nodes, so the
#   output is a small FBX with no texture.
# - Renames the mesh "Kiekko" to "Puck" and the materials "Jääkiekko"/"Jääkiekko reuna" to "Puck Face"/"Puck Rim"
#   (ASCII names). Both stay near-black (base colour 0.01).
# - Bakes the axis change into the mesh (bake_space_transform), so in Unity the puck's thin axis is the mesh's own Y
#   with no rotation on the model root. The game fits the model to its puck size and keeps the flat face on local Y.
import argparse, os, sys
import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--in", dest="src", required=True, help="the supplied hockey_puck.fbx")
parser.add_argument("--out", required=True, help="output FBX, e.g. Assets/Resources/Models/puck.fbx")
args = parser.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.abspath(args.src))

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if len(meshes) != 1:
    sys.exit("expected one mesh in %s, found %d" % (args.src, len(meshes)))
puck = meshes[0]
for o in list(bpy.data.objects):
    if o is not puck:
        bpy.data.objects.remove(o, do_unlink=True)
puck.name = "Puck"
puck.data.name = "Puck"

names = {"Jääkiekko": "Puck Face", "Jääkiekko reuna": "Puck Rim"}
for slot in puck.material_slots:
    mat = slot.material
    if mat is None:
        continue
    mat.name = names.get(mat.name, "Puck Rim" if "reuna" in mat.name else "Puck Face")
    if mat.use_nodes:
        nodes = mat.node_tree.nodes
        for n in list(nodes):
            if n.type in {"TEX_IMAGE", "NORMAL_MAP", "BUMP"}:
                nodes.remove(n)
for img in list(bpy.data.images):
    bpy.data.images.remove(img)

# Apply any object transform so the mesh data holds the shape.
bpy.ops.object.select_all(action="DESELECT")
puck.select_set(True)
bpy.context.view_layer.objects.active = puck
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

out = os.path.abspath(args.out)
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=False, object_types={"MESH"}, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
    bake_anim=False, path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE", use_mesh_modifiers=True)
print("PUCK wrote", out, "materials", [s.material.name for s in puck.material_slots if s.material])
