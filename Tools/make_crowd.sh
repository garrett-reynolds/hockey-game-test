#!/usr/bin/env bash
# Builds all four crowd body files (two bodies x near and far level) with Tools/make_crowd.py (Blender, headless).
#   bash Tools/make_crowd.sh "<short folder with Superhero_Male_FullBody.fbx, Superhero_Female_FullBody.fbx and hair/>"
# The folder holds the Quaternius Universal Base Characters (Standard) files: Base Characters/Unity/*.fbx and the
# Hairstyles/Origin at 0/FBX (Unity)/*.fbx files in hair/. Pass a Windows path (C:/...).
set -euo pipefail
SRC="$1"
BLENDER="/c/Program Files/Blender Foundation/Blender 4.5/blender.exe"
OUT="$(cd "$(dirname "$0")/.." && pwd -W)/Assets/Resources/Crowd"
run() {  # body hairs out triangles [--card]
  "$BLENDER" -b --factory-startup --disable-autoexec --python-exit-code 1 --python "$(dirname "$0")/make_crowd.py" -- \
    --body "$SRC/$1" --hair "$SRC/hair" --hairs "$2" --out "$OUT/$3.bytes" --triangles "$4" ${5:-} | grep -E "body |hair |with hair|written"
}
MALE=Hair_Buzzed,Hair_SimpleParted,Hair_Beard
FEMALE=Hair_BuzzedFemale,Hair_Long,Hair_Buns
run Superhero_Male_FullBody.fbx "$MALE" fan-male-near 1500
run Superhero_Male_FullBody.fbx "$MALE" fan-male-far 400 --card
run Superhero_Female_FullBody.fbx "$FEMALE" fan-female-near 1500
run Superhero_Female_FullBody.fbx "$FEMALE" fan-female-far 400 --card
