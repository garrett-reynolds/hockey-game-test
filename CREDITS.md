# Credits

Every model, texture, and sound in the game that was not made in code. Each entry gives what it is, its file in the project, source, author, and licence.

Models load from `Assets/Resources/Models/` through `ModelFiles`. Sounds load from `Assets/Resources/Sounds/` through `SoundFiles`. A missing file falls back to the code-made placeholder.

## Models

| What | File in project | Source | Author | Licence |
|---|---|---|---|---|
| Hockey puck (low poly, chamfered edges, ridged grip band, 1,536 triangles) | `Assets/Resources/Models/puck.obj`, `puck.mtl` | Made for this project by `Tools/make_puck.py` (run: `python Tools/make_puck.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |
| Hockey net (red posts and crossbar, white padded base frame, white strand netting, 4,708 triangles) | `Assets/Resources/Models/net.obj`, `net.mtl` | Made for this project by `Tools/make_net.py` (run: `python Tools/make_net.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |
| Goal light (grey housing, red dome, 628 triangles) | `Assets/Resources/Models/goal-light.obj`, `goal-light.mtl` | Made for this project by `Tools/make_net.py` (same run as the net), no outside source | Hockey Game Test project | CC0 |

### Notes

- **Puck.** No free (CC0 or CC-BY) puck could be downloaded without a login. Sketchfab has CC-BY pucks, but its download needs a signed-in account, so a simple puck was built instead. If a better puck is wanted, these CC-BY models can be downloaded by hand from a Sketchfab account, exported as FBX or OBJ, and dropped in as `Assets/Resources/Models/puck.fbx` (delete `puck.obj` first). The game scales any puck model to its fixed puck size. Credit the author here if you do.
  - "Hockey Puck" by j.thorne: https://sketchfab.com/3d-models/716ae43cc57a453a9e830cef1e55b47c (CC Attribution)
  - "Hockey Puck for the PencilTip Hockey Stick" by hello.3d: https://sketchfab.com/3d-models/7de7570b1ff847e1a3a2eda3077d14d6 (CC Attribution)
- **Net and goal light.** No free (CC0 or CC-BY) hockey net or goal light could be downloaded without a login (Poly Pizza, OpenGameArt, Kenney and Quaternius were checked; Sketchfab needs an account), so both were built instead. The game fits any net model's outer box to the posts' outer edges, the crossbar top, and the posts' front to the back of the net (0.6 m), so a replacement net must have its posts 0.08 m thick, just outside a 1.83 m x 1.22 m opening, and nothing behind its back. Name the netting part `Netting` so it shakes on a goal, and the goal light's lamp part `Dome` so it lights up. These CC-BY nets can be downloaded by hand from a Sketchfab account:
  - "Hockey Net" by imveryokay: https://sketchfab.com/3d-models/hockey-net-04f1b2bbebe4419b8c894c113cf864d9 (CC Attribution)
  - "Hockey goal" by voittohovi: https://sketchfab.com/3d-models/hockey-goal-42e1d6e7eff54fdf92439638630106d6 (CC Attribution)

## Textures

None yet.

## Sounds

None yet.
