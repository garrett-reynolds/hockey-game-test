#!/usr/bin/env bash
# Builds all 24 crowd body files (12 Quaternius Modular outfits x near and far level) with Tools/make_crowd.py (Blender, headless).
#   bash Tools/make_crowd.sh "<short folder with men/ and women/>"
# The folder holds the 12 outfit FBX files from Quaternius "Ultimate Modular Men" (Feb 2022) and "Ultimate Modular Women"
# (April 2022), Individual Characters/FBX (CC0): men/{Casual_Hoodie,Casual_2,Suit,Worker,Punk,Farmer}.fbx and
# women/{Casual,Suit,Formal,Worker,Punk,Adventurer}.fbx. The packs share file names, so men and women stay in two folders.
# Pass a Windows path (C:/...). Output: Assets/Resources/Crowd/fan-<m|w>-<outfit>-<near|far>.bytes (file format version 3).
set -euo pipefail
SRC="$1"
BLENDER="/c/Program Files/Blender Foundation/Blender 4.5/blender.exe"
OUT="$(cd "$(dirname "$0")/.." && pwd -W)/Assets/Resources/Crowd"
run() {  # outfit file (men/Casual_Hoodie), output name (fan-m-casual-hoodie-near), triangles, [--card]
  "$BLENDER" -b --factory-startup --disable-autoexec --python-exit-code 1 --python "$(dirname "$0")/make_crowd.py" -- \
    --outfit "$SRC/$1.fbx" --out "$OUT/$2.bytes" --triangles "$3" ${4:-} | grep -E "outfit |CHECK|written"
}
build() {  # outfit file, output key (m-casual-hoodie)
  run "$1" "fan-$2-near" 1500
  run "$1" "fan-$2-far" 400 --card
}
build men/Casual_Hoodie m-casual-hoodie
build men/Casual_2 m-casual-2
build men/Suit m-suit
build men/Worker m-worker
build men/Punk m-punk
build men/Farmer m-farmer
build women/Casual w-casual
build women/Suit w-suit
build women/Formal w-formal
build women/Worker w-worker
build women/Punk w-punk
build women/Adventurer w-adventurer
