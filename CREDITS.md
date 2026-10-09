# Credits

Every model, texture, and sound in the game that was not made in code. Each entry gives what it is, its file in the project, source, author, and licence.

Models load from `Assets/Resources/Models/` through `ModelFiles`. Sounds load from `Assets/Resources/Sounds/` through `SoundFiles`. A missing file falls back to the code-made placeholder.

## Models

| What | File in project | Source | Author | Licence |
|---|---|---|---|---|
| Hockey puck (low poly, chamfered edges, ridged grip band, 1,536 triangles) | `Assets/Resources/Models/puck.obj`, `puck.mtl` | Made for this project by `Tools/make_puck.py` (run: `python Tools/make_puck.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |
| Ice sheet (one flat 60 m square, textured with the rink markings below) | `Assets/Resources/Models/ice.obj`, `ice.mtl` | Made for this project by `Tools/make_ice.py` (run: `python Tools/make_ice.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |
| Hockey net (red posts and crossbar, white padded base frame, white strand netting, 4,708 triangles) | `Assets/Resources/Models/net.obj`, `net.mtl` | Made for this project by `Tools/make_net.py` (run: `python Tools/make_net.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |
| Goal light (grey housing, red dome, 628 triangles) | `Assets/Resources/Models/goal-light.obj`, `goal-light.mtl` | Made for this project by `Tools/make_net.py` (same run as the net), no outside source | Hockey Game Test project | CC0 |
| Goalie character (rigged low-poly humanoid, Humanoid avatar, bones Hips/Spine/Chest/Neck/Head, arms, legs, fingers) | `Assets/Resources/Models/goalie-dressing.fbx` (copy of `Model/characterMedium.fbx`, unchanged) | Kenney "Animated Characters: Protagonists" 1.1, https://kenney.nl/assets/animated-characters-protagonists | Kenney (www.kenney.nl) | CC0 (`License.txt` in the zip) |
| Goalie leg pads, blocker, mask (white with blue bands, dark cage) | `Assets/Resources/Models/goalie-pad.obj`, `goalie-blocker.obj`, `goalie-mask.obj` and their `.mtl` | Made for this project by `Tools/make_goalie_gear.py` (run: `python Tools/make_goalie_gear.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |
| Save glove / goalie catcher (trapper with tan pocket, T web and cuff); used for the SAVE block and on the goalie character's left hand | `Assets/Resources/Models/save-glove.obj`, `save-glove.mtl` | Made for this project by `Tools/make_goalie_gear.py` (same run as the gear), no outside source | Hockey Game Test project | CC0 |
| Rink boards and glass (white boards with a yellow kick plate and blue top rail, see-through glass panels 2.43 m high round the ends and 1.83 m along the sides, thin stanchions; NHL rink size, 1,632 triangles) | `Assets/Resources/Models/boards.obj`, `boards.mtl` | Made for this project by `Tools/make_arena.py` (run: `py -3 Tools/make_arena.py Assets/Resources/Models`), no outside source | Hockey Game Test project | CC0 |
| Arena (dark floor round the boards, two tiers of seats with fans, a glowing LED ribbon board between the tiers, back wall and roof, 14,014 triangles) | `Assets/Resources/Models/arena.obj`, `arena.mtl` | Made for this project by `Tools/make_arena.py` (same run as the boards), no outside source | Hockey Game Test project | CC0 |

### Notes

- **Puck.** No free (CC0 or CC-BY) puck could be downloaded without a login. Sketchfab has CC-BY pucks, but its download needs a signed-in account, so a simple puck was built instead. If a better puck is wanted, these CC-BY models can be downloaded by hand from a Sketchfab account, exported as FBX or OBJ, and dropped in as `Assets/Resources/Models/puck.fbx` (delete `puck.obj` first). The game scales any puck model to its fixed puck size. Credit the author here if you do.
  - "Hockey Puck" by j.thorne: https://sketchfab.com/3d-models/716ae43cc57a453a9e830cef1e55b47c (CC Attribution)
  - "Hockey Puck for the PencilTip Hockey Stick" by hello.3d: https://sketchfab.com/3d-models/7de7570b1ff847e1a3a2eda3077d14d6 (CC Attribution)
- **Net and goal light.** No free (CC0 or CC-BY) hockey net or goal light could be downloaded without a login (Poly Pizza, OpenGameArt, Kenney and Quaternius were checked; Sketchfab needs an account), so both were built instead. The game fits any net model's outer box to the posts' outer edges, the crossbar top, and the posts' front to the back of the net (0.6 m), so a replacement net must have its posts 0.08 m thick, just outside a 1.83 m x 1.22 m opening, and nothing behind its back. Name the netting part `Netting` so it shakes on a goal, and the goal light's lamp part `Dome` so it lights up. These CC-BY nets can be downloaded by hand from a Sketchfab account:
  - "Hockey Net" by imveryokay: https://sketchfab.com/3d-models/hockey-net-04f1b2bbebe4419b8c894c113cf864d9 (CC Attribution)
  - "Hockey goal" by voittohovi: https://sketchfab.com/3d-models/hockey-goal-42e1d6e7eff54fdf92439638630106d6 (CC Attribution)

- **Goalie character and gear.** No free, login-free rigged hockey goalie was found (checked: Kenney and Quaternius have free rigged humanoids but no goalie; the Sketchfab goalie needs a login and is probably unrigged; Mixamo needs an Adobe login; the Unity Asset Store goalie is paid). So Kenney's CC0 rigged character is used, imported as a Unity Humanoid, wearing a repainted jersey skin. No free, login-free goalie pad, blocker, catcher or mask mesh was found (the Sketchfab ones need a login and are 35k to 435k faces; the CC0 Quaternius "Glove" on Poly Pizza is GLB only and likely a plain glove), so simple gear was built and is parented to the character's bones in code (`GoalieCharacter`). The character only shows when the developer flag `goalieCharacterOn=true` is in the settings file.

- **Boards, glass and arena.** No free (CC0 or CC-BY) boards or glass model could be downloaded without a login, so both the boards and the arena were built instead. Checked: Poly Pizza (its stands models are GLB only, which this project cannot import without adding glTFast), Sketchfab (login needed). The Poly Haven `circus_arena` HDRI (CC0) was considered for the backdrop but not used: the built stands and roof close the arena on their own. If a modelled rink is wanted, this CC-BY model can be downloaded by hand (it would need its glass part named `Glass` and its size matched to `RinkBoards.Box`):
  - "Hockey Rink With UV Map" by rockleyr: https://sketchfab.com/3d-models/hockey-rink-eeaaf38b50e748feb226a7bdbdcb9138 (CC Attribution)

## Textures

| What | File in project | Source | Author | Licence | Changes |
|---|---|---|---|---|---|
| Ice markings: white ice with faint skate scuffs, red goal line, blue line, red centre line, blue crease with red outline, faceoff circles and dots (NHL sizes, lined up with the net) | `Assets/Resources/Models/ice-markings.jpg` | Made for this project by `Tools/make_ice_texture.py` (needs Pillow and numpy; run: `python Tools/make_ice_texture.py <folder>/Ice002_1K-JPG_Roughness.jpg Assets/Resources/Models`). The scuffs are the roughness map below, tiled every 3 m and made near-white. | Hockey Game Test project | CC0 | Made for this project |
| Ice002 (ice surface, roughness map used for the scuffs) | Baked into `ice-markings.jpg` (not stored on its own) | https://ambientcg.com/view?id=Ice002 , download https://ambientcg.com/get?file=Ice002_1K-JPG.zip | ambientCG | CC0 1.0 | Roughness map tiled every 3 m, made near-white, baked into the markings |
| Goalie jersey skin for the goalie character | `Assets/Resources/Models/goalie-skin.png` | Kenney "Animated Characters: Protagonists" 1.1, https://kenney.nl/assets/animated-characters-protagonists (`Skins/skaterMaleA.png`) | Kenney (www.kenney.nl) | CC0 | Shirt repainted as a blue goalie jersey with white stripes and crest, jeans repainted as dark pants, by `Tools/make_goalie_skin.py` (run: `python Tools/make_goalie_skin.py <Kenney zip>/Skins/skaterMaleA.png Assets/Resources/Models`) |
| Crowd of seated fans on the arena seats (one row of 16 seats, repeated round the stands) | `Assets/Resources/Models/crowd.png` | Made for this project by `Tools/make_arena.py`, no outside source | Hockey Game Test project | CC0 | Drawn in code |

### Notes

- **Ice.** No free rink-ice model with markings could be downloaded without a login. Most rink diagrams on Wikimedia Commons are CC BY-SA, and the public-domain one (Ice_hockey_layout.svg) is a whole-rink drawing that cannot be lined up with the net, so the markings are drawn by the scripts above from NHL sizes. The markings are a texture rather than separate meshes so that far lines fade smoothly instead of breaking into dots. Delete `ice.obj` to get the plain placeholder ice back.

## Sounds

All 12 are 16-bit PCM WAV, 44.1 kHz, in `Assets/Resources/Sounds/`. The one-shots are normalised to a -3 dBFS peak. Every outside clip was decoded with ffmpeg and then trimmed, mixed, or had its level changed as listed below. Clips marked "made by `Tools/make_sounds.py`" come from this command, run from the repo root:

`python Tools/make_sounds.py Assets/Resources/Sounds --voice ooo.wav --crowd crowd.wav`

Its two inputs are decoded first with `ffmpeg -i oooooooooo.ogg -ac 1 -ar 44100 ooo.wav` and `ffmpeg -i crowd_shouting_0.ogg -ar 44100 crowd.wav`.

| What | File in project | Source | Author | Licence | Changes |
|---|---|---|---|---|---|
| Crowd ambience loop | `crowd-loop.wav` | "Crowd Shouting/Speaking Ambience", https://opengameart.org/content/crowd-shoutingspeaking-ambience (`crowd_shouting_0.ogg`) | StarNinjas | CC0 | Made by `Tools/make_sounds.py`: a 16 s cut starting 2 s in, with a 1.5 s equal-power crossfade of the tail into the head so the loop is seamless, then lowered 6 dB |
| Heartbeat loop | `heartbeat-loop.wav` | "Heartbeat sounds", https://opengameart.org/content/heartbeat-sounds (`heartbeat_slow_0.wav`) | bart | CC0 | Level raised only. It is already a two-beat loop that ends in silence |
| Window tick | `tick.wav` | Kenney "Interface Sounds", https://kenney.nl/assets/interface-sounds (`tick_004.ogg`) | Kenney | CC0 | Level changed |
| Fast tick | `fast-tick.wav` | Kenney "Interface Sounds" (`tick_002.ogg`) | Kenney | CC0 | Level changed |
| Net hit | `net-hit.wav` | Kenney "Impact Sounds", https://kenney.nl/assets/impact-sounds (`impactSoft_medium_000.ogg` plus `impactPlate_light_000.ogg` at 30%) | Kenney | CC0 | Two clips mixed |
| Pad thud | `pad-thud.wav` | Kenney "Impact Sounds" (`impactSoft_heavy_001.ogg`) | Kenney | CC0 | Level changed |
| Boards clang | `boards-clang.wav` | Kenney "Impact Sounds" (`impactWood_heavy_000.ogg`, `impactPlank_medium_001.ogg` at 60%, `impactMetal_light_000.ogg` at 40%) | Kenney | CC0 | Three clips mixed |
| Cheer after a Shot | `cheer.wav` | "Free Crowd Cheering Sounds", https://opengameart.org/content/free-crowd-cheering-sounds ("03 - Strong cheering - I") | Gregor Quendel | CC-BY 4.0 (https://creativecommons.org/licenses/by/4.0/) | Cut 5.0 to 8.0 s, 0.15 s fade in, 0.6 s fade out, normalised, MP3 converted to WAV |
| New-best cheer | `new-best-cheer.wav` | "Free Crowd Cheering Sounds" ("01 - Strong cheering and strong rhythmic cheering") | Gregor Quendel | CC-BY 4.0 | Cut 4.5 to 12.5 s, 0.2 s fade in, 2 s fade out, normalised, MP3 converted to WAV |
| Crowd groan ("awww") | `groan.wav` | Made by `Tools/make_sounds.py` from "Oooooooooooooo", https://opengameart.org/content/oooooooooooooo (`oooooooooo.ogg`) | Nocturnal_Vanguard (voice); Hockey Game Test project (script) | CC0 | 8 staggered, detuned, panned copies of the voice, each gliding down 3 to 4.5 semitones, low-passed, 2 s |
| Goal horn | `goal-horn.wav` | Made by `Tools/make_sounds.py` (synthesised: a low B-flat major chord of detuned sawtooth waves with a short echo) | Hockey Game Test project | CC0 | n/a |
| Time-up buzzer | `buzzer.wav` | Made by `Tools/make_sounds.py` (synthesised: a rough square plus sawtooth wave around 120 Hz) | Hockey Game Test project | CC0 | n/a |

### Notes

- **Goal horn, buzzer, groan.** No free goal horn, arena buzzer, or crowd groan could be downloaded without a login (Freesound needs an account, and Pixabay blocks scripted downloads). These clips were made instead. To swap in a real recording, replace the WAV with a file of the same name. Keep exactly one file per name, because the game ignores the extension. Then credit the new clip here.
