# Jersey and net replay result

Both runtime changes are installed in `C:\Users\garrett.reynolds\Projects\Personal\hockey-wt\111`.

The user saved and closed Unity before installation. All eight installed files match the checked source hashes. The four existing files have byte-for-byte backups. The recent crowd performance files were not touched.

## Changes

- Jersey: cached knit/fold texture, original compass crest on the front, block number 31 on the back. Existing skin weights and triangle count remain. No added renderer or drawing pass. Pants/socks keep their old UVs, texture, and color with their existing matte finish.
- Net: record actual cloth vertex shapes at up to 30 Hz, then replay them on the puck's shot clock. The recording has a fixed 256-sample bound. It does not run a second cloth solver. The net cords are darker against the ice. The replay stays at 512 by 280.

User confirms the net, crest, and number look good. The user wants stronger cloth and less chest/shoulder bulk. Follow-up is in progress: add knitted normal relief and reduce maker-added padding while preserving the bones. This follow-up needs its own code review and a fresh user run. Do not reuse the prior Unity test grant.

## Proof

Final controlled run `bf20208a6fbf40f594a21c8a9ce60daa` ended successfully. All three phases returned 0: jersey capture 20.718 seconds, six clothes checks 19.047 seconds, native net check 23.485 seconds.

Together with the unchanged replay/net checks from run 01, 30 focused EditMode checks and one real-cloth fixture passed. No heavy run is still active for this work.

The native fixture used two impact points and a translated/rotated net. Stored and played vertices matched within 0.00001 m. Peak movement was 0.139574 m and 0.144794 m. Net cords remain visible against the light ice in the saved board-size images.

The fixture measured mean record cost of 0.0578 and 0.0094 ms, and mean apply cost of 0.3015 and 0.0683 ms. Each 31-sample demonstration stored 564696 bytes of vertex positions, excluding array overhead. These are controlled fixture costs, not a full-game frame benchmark.

The back image shows number 31 and the cloth detail. The first front capture shows an incomplete material result, so it does not prove the front crest's final in-game appearance. The front crest and overall visual quality still require a user play check. Do not close either ticket yet. No full-game before/after video or live FPS result is claimed.

## Files and recovery

- Integration: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\source`
- Applied runtime manifest: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\runtime-delivery.json`
- Old runtime files: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\live-before-visual`
- Checked runtime copies: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\runtime-ready`
- Final test output: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\native-proof-03`
- Old wider jersey views: `C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\native-proof-02\jersey-before`

Before any revert, close Unity and match every current file to its installed hash. Restore only the four backed-up files and remove only the four new files listed in the manifest. Do not reset the project or touch other edits.

No commits, push, or PR were made for this work.
