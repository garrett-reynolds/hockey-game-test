# Offline picture pair

Public entry point: `compare_pair(old_manifest_path, new_manifest_path)` in
`Tools/picture_pair_compare.py`. It returns a report. Invalid input returns a
failed report with `problems`. It never starts Unity or writes source files.

This file defines the capture writer input shape. It does not prove a capture.
The public entry point requires 1280 by 720 pixels. Small pure tests use the
private whole-job engine with smaller images and the same shape and math.
The small tests do not prove the public full-size success path.
A full-size comparison still needs its own controlled run and real captures.
The public entry point pins old source to
`0a31aeff3ef4a2509a74f672105dda8575ee043a` and new source to
`48bfa52e2205f8fc97ee77d458c97d991cba3689`.
Change that freeze only through a reviewed contract revision before capture.
Valid hash text records provenance. It does not prove the allowed source edits.
The controlled capture plan must bind the actual source and payload files.

Each JSON manifest uses `schema: hockey-picture-capture-v1`, `width`, `height`,
`sourceCommit`, `sourceDiffSha256`, `capturePayloadSha256`, `frozen`, and `views`.
The first hash identifies source edits. The second identifies the shared capture
payload. Each is a SHA256 string. Source commits are full 40-character hashes.

`frozen` contains these required fields: `unityVersion`, `host`, `gpu`,
`gpuDriver`, `graphicsApi`, `colorSpace`, `qualitySettings`, `renderSettings`,
`crowdDrawn`, `poseInputs`, `targetFormat`, `targetReadWrite`, `antiAliasing`.
Use real typed values. Both captures must have equal frozen values.

Each view contains `id`, `camera`, `image`, `masks`, and `gaps`.
The six IDs are `left-cheat-easy-on`, `left-cheat-easy-off`,
`middle-depth-easy-on`, `middle-depth-easy-off`, `right-late-settle-easy-on`,
and `right-late-settle-easy-off`.
`camera` contains `cameraPosition`, `cameraRotation`, `cameraProjection`, and
`cameraFieldOfView`. Camera values must match for each view.

`image` describes raw RGBA32 bytes in Unity bottom-left row order.
Each file entry contains `path`, `byteCount`, and `sha256`.
Paths are relative to the manifest folder and must stay in that folder.
The image byte count is width times height times four.

`masks` contains `goalie`, `glow`, `gearSeen`, `calm`, and `gear`.
Each mask entry also contains `trueCount`.
Each mask has one byte per pixel. Only 0 and 1 are valid.
The mask byte count is width times height.

Each old Gap contains `id`, `cellIndices`, `projectedCellRegionMask`, and
`gearExcludedMask`. Both mask fields use the same file entry shape.
Cell indices are distinct non-negative integers in sorted order.
The new capture can list its own Gap IDs as extra evidence.
The comparison never uses new Gap IDs to find old samples.

The common report uses the old Gap ID and cell list.
Calm is the intersection of both Calm masks.
Gear is the intersection of both gear masks.
Each fixed Gap uses its old projected region minus both gearSeen masks.
The same resulting mask measures both frames.
An empty valid sample fails. A no-Gap view still gets both Calm checks.

The math follows `Assets/Scripts/Game/Editor/PictureMeasures.cs` and
`Assets/Scripts/Game/Editor/PictureCompare.cs`.
Contrast is luma standard deviation.
Edges use Sobel magnitude divided by four, with threshold 0.1.
All nine pixels must be in the mask for an edge sample.
Gap stand-out is the absolute difference of Gap and gear mean luma.
Higher contrast or edges is worse. Lower stand-out is worse.
More than 10 percent worse fails. Exactly 10 percent passes.
Any worse result from a zero baseline fails.

Keep the seven original baseline files and hashes unchanged.
Keep the earlier invalid comparison and its +15.2083 percent evidence.
This tool does not replace that evidence or close ticket #97.
