# Tools

Scripts that make the game's models, textures and sounds. Each script's first lines give its exact command.
Run them from the repo root. `CREDITS.md` says which script made which file.

## Plain Python 3

Install the packages once: `py -3 -m pip install -r Tools/requirements.txt` (Pillow and numpy).

| Script | Makes | Needs |
|---|---|---|
| `make_ads.py` | the board ad and ice logo art in `Assets/Resources/Ads` (`py -3 Tools/make_ads.py Assets/Resources/Ads C:/Windows/Fonts`) | Pillow, Windows fonts |
| `make_arena.py` | `boards.obj`, `arena.obj` (+ `.mtl`) | none |
| `make_goal_light.py` | `goal-light.obj` (+ `.mtl`) | - |
| `make_goalie_gear.py` | `save-glove.obj` (+ `.mtl`) | - |
| `make_ice.py` | `ice.obj` (+ `.mtl`) | - |
| `make_ice_texture.py` | `ice-markings.jpg` | Pillow, numpy |
| `make_sounds.py` | the made-for-this-project sounds | numpy |
| `cut_stride_sounds.py` | `stride-1.wav` .. `stride-4.wav`, `hockey-stop.wav` (cut from the #35 skating recording; see the script) | numpy |

Example: `py -3 Tools/make_arena.py Assets/Resources/Models`

`vector3.py` holds the vector helpers the model scripts share; it is not run on its own.

## Blender 4.5 (headless)

These run inside Blender 4.5 (`bpy`), not plain Python. Each needs a downloaded or supplied file as input:

| Script | Makes |
|---|---|
| `make_crowd.py` | one 3D crowd fan body at one level, with its body shapes, hair styles, clips and (far) card pictures (`Assets/Resources/Crowd/*.bytes`), from a Quaternius base character and its hair; `make_crowd.sh` makes all four |
| `make_lifelike_goalie.py` | `goalie-dressing.fbx` and its textures |
| `make_supplied_net.py` | `net.fbx` |
| `make_supplied_puck.py` | `puck.fbx` |

Run from Git Bash, for example:

```bash
"/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --disable-autoexec \
  --python Tools/make_supplied_puck.py -- <arguments at the top of the script>
```

`make_supplied_net.py` also uses numpy; Blender 4.5 ships with it.
