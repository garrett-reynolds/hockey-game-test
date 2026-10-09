# Credits

Every model, texture, and sound in the game that was not made in code. Each entry gives what it is, its file in the project, source, author, and licence.

Models load from `Assets/Resources/Models/` through `ModelFiles`. Sounds load from `Assets/Resources/Sounds/` through `SoundFiles`. A missing file falls back to the code-made placeholder.

## Models

| What | File in project | Source | Author | Licence |
|---|---|---|---|---|
| Hockey puck (low poly, chamfered edges, ridged grip band, 1,536 triangles) | `Assets/Resources/Models/puck.obj`, `puck.mtl` | Made for this project by `Tools/make_puck.py` (run: `python Tools/make_puck.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |
| Ice sheet (one flat 60 m square, textured with the rink markings below) | `Assets/Resources/Models/ice.obj`, `ice.mtl` | Made for this project by `Tools/make_ice.py` (run: `python Tools/make_ice.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |

### Notes

- **Puck.** No free (CC0 or CC-BY) puck could be downloaded without a login. Sketchfab has CC-BY pucks, but its download needs a signed-in account, so a simple puck was built instead. If a better puck is wanted, these CC-BY models can be downloaded by hand from a Sketchfab account, exported as FBX or OBJ, and dropped in as `Assets/Resources/Models/puck.fbx` (delete `puck.obj` first). The game scales any puck model to its fixed puck size. Credit the author here if you do.
  - "Hockey Puck" by j.thorne: https://sketchfab.com/3d-models/716ae43cc57a453a9e830cef1e55b47c (CC Attribution)
  - "Hockey Puck for the PencilTip Hockey Stick" by hello.3d: https://sketchfab.com/3d-models/7de7570b1ff847e1a3a2eda3077d14d6 (CC Attribution)

## Textures

| What | File in project | Source | Author | Licence |
|---|---|---|---|---|
| Ice markings: white ice with faint skate scuffs, red goal line, blue line, red centre line, blue crease with red outline, faceoff circles and dots (NHL sizes, lined up with the net) | `Assets/Resources/Models/ice-markings.jpg` | Made for this project by `Tools/make_ice_texture.py` (needs Pillow and numpy; run: `python Tools/make_ice_texture.py <folder>/Ice002_1K-JPG_Roughness.jpg Assets/Resources/Models`). The scuffs are the roughness map below, tiled every 3 m and made near-white. | Hockey Game Test project | CC0 |
| Ice002 (ice surface, roughness map used for the scuffs) | Baked into `ice-markings.jpg` (not stored on its own) | https://ambientcg.com/view?id=Ice002 , download https://ambientcg.com/get?file=Ice002_1K-JPG.zip | ambientCG | CC0 1.0 |

### Notes

- **Ice.** No free rink-ice model with markings could be downloaded without a login. Most rink diagrams on Wikimedia Commons are CC BY-SA, and the public-domain one (Ice_hockey_layout.svg) is a whole-rink drawing that cannot be lined up with the net, so the markings are drawn by the scripts above from NHL sizes. The markings are a texture rather than separate meshes so that far lines fade smoothly instead of breaking into dots. Delete `ice.obj` to get the plain placeholder ice back.

## Sounds

None yet.
