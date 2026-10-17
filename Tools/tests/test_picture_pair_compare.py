"""Small offline checks at the manifest-to-report boundary."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("picture_pair_compare", Path(__file__).parents[1] / "picture_pair_compare.py")


class PairTests(unittest.TestCase):
    def test_public_entry_pins_source_commits_before_loading_pixels(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            old, new = make_pair(Path(folder))
            for path in (old, new):
                data = json.loads(path.read_text())
                data.update(width=1280, height=720)
                path.write_text(json.dumps(data))
            report = module.compare_pair(old, new)
            self.assertFalse(report["pass"])
            self.assertIn("source commits differ from frozen pair contract", report["problems"])

    def test_public_entry_rejects_small_fixture_dimensions(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            old, new = make_pair(Path(folder))
            report = module.compare_pair(old, new)
            self.assertFalse(report["pass"])
            self.assertIn("production dimensions must be 1280x720", report["problems"])

    def test_calm_uses_same_intersection_for_both_frames(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            old, new = make_pair(Path(folder))
            data = json.loads(new.read_text())
            image = bytearray((new.parent / "frame.rgba").read_bytes())
            # The new Calm excludes the top row. Its changed pixels must not
            # enter either common contrast sample.
            image[56 * 4:] = bytes([255, 255, 255, 255] * 8)
            mask = bytes([1] * 56 + [0] * 8)
            (new.parent / "frame.rgba").write_bytes(image)
            (new.parent / "calm.mask").write_bytes(mask)
            for view in data["views"]:
                view["image"]["sha256"] = hashlib.sha256(image).hexdigest()
                view["masks"]["calm"] = {"path": "calm.mask", "byteCount": 64, "sha256": hashlib.sha256(mask).hexdigest(), "trueCount": 56}
            new.write_text(json.dumps(data))
            report = module._compare_pair(old, new)
            self.assertTrue(report["pass"], report)
            self.assertEqual(56, report["views"][0]["commonCalm"]["old"]["pixels"])
            self.assertEqual(56, report["views"][0]["commonCalm"]["new"]["pixels"])

    def test_source_measures_and_provenance_remain_in_report(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            old, new = make_pair(Path(folder))
            report = module._compare_pair(old, new)
            self.assertEqual(64, report["views"][0]["sourceMeasures"]["old"]["calm"]["pixels"])
            self.assertEqual("a" * 40, report["sources"]["old"]["sourceCommit"])

    def test_sobel_ten_percent_passes_and_twenty_percent_fails(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        for new_edges, expected in ((11, True), (12, False)):
            with self.subTest(new_edges=new_edges), tempfile.TemporaryDirectory() as folder:
                old, new = make_pair(Path(folder))
                for path, edge_count in ((old, 10), (new, new_edges)):
                    data = json.loads(path.read_text())
                    data["width"], data["height"] = 400, 3
                    mask = bytearray(1200)
                    rgba = bytearray([0, 0, 0, 255] * 1200)
                    # Each separate 3x3 island has exactly one interior sample.
                    # A white right column gives Sobel magnitude 1 at its centre.
                    for island in range(100):
                        for y in range(3):
                            for x in range(3):
                                index = y * 400 + island * 4 + x
                                mask[index] = 1
                                if island < edge_count and x == 2:
                                    rgba[index * 4:index * 4 + 3] = bytes([255] * 3)
                    for name, value in (("frame.rgba", rgba), ("full.mask", mask)):
                        (path.parent / name).write_bytes(value)
                    for view in data["views"]:
                        for entry, value in [(view["image"], rgba)] + [(v, mask) for v in view["masks"].values()]:
                            entry.update(byteCount=len(value), sha256=hashlib.sha256(value).hexdigest())
                            if "trueCount" in entry:
                                entry["trueCount"] = sum(value)
                    path.write_text(json.dumps(data))
                report = module._compare_pair(old, new)
                self.assertEqual(expected, report["pass"], report)
                edges = [c for c in report["checks"] if c["metric"] == "edges"]
                self.assertEqual(6, len(edges))
                self.assertAlmostEqual(.1, edges[0]["old"])
                self.assertAlmostEqual(new_edges / 100, edges[0]["new"])

    def test_missing_and_empty_inputs_fail(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        for kind in ("view", "mask", "hash", "camera", "interior"):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as folder:
                old, new = make_pair(Path(folder))
                data = json.loads(new.read_text())
                if kind == "view":
                    data["views"].pop()
                elif kind == "mask":
                    del data["views"][0]["masks"]["calm"]
                elif kind == "hash":
                    data["views"][0]["image"]["sha256"] = "0" * 64
                elif kind == "camera":
                    del data["views"][0]["camera"]["cameraPosition"]
                else:
                    blank = bytes(64)
                    (new.parent / "empty.mask").write_bytes(blank)
                    data["views"][0]["masks"]["calm"] = {"path": "empty.mask", "byteCount": 64, "sha256": hashlib.sha256(blank).hexdigest(), "trueCount": 0}
                new.write_text(json.dumps(data))
                report = module._compare_pair(old, new)
                self.assertFalse(report["pass"])
                self.assertTrue(report["problems"])

    def test_empty_gap_or_gear_and_invalid_mask_fail(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        for kind in ("gap", "gear", "nonbinary", "dimensions", "frozen", "cell-list", "malformed"):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as folder:
                old, new = make_pair(Path(folder))
                data = json.loads(old.read_text())
                full = data["views"][0]["masks"]["calm"]
                blank = bytes(64)
                (old.parent / "empty.mask").write_bytes(blank)
                empty = {"path": "empty.mask", "byteCount": 64, "sha256": hashlib.sha256(blank).hexdigest(), "trueCount": 0}
                gap = {"id": "old-gap", "cellIndices": [1], "projectedCellRegionMask": full, "gearExcludedMask": full}
                for view in data["views"]:
                    view["gaps"] = [gap]
                if kind == "gap":
                    # Full gearSeen removes every fixed Gap sample.
                    pass
                elif kind == "gear":
                    data["views"][0]["masks"]["gearSeen"] = empty
                    data["views"][0]["masks"]["gear"] = empty
                    other = json.loads(new.read_text())
                    other["views"][0]["masks"]["gearSeen"] = empty
                    (new.parent / "empty.mask").write_bytes(blank)
                    new.write_text(json.dumps(other))
                elif kind == "nonbinary":
                    invalid = bytes([2]) + bytes([1] * 63)
                    (old.parent / "invalid.mask").write_bytes(invalid)
                    data["views"][0]["masks"]["glow"] = {"path": "invalid.mask", "byteCount": 64, "sha256": hashlib.sha256(invalid).hexdigest(), "trueCount": 65}
                elif kind == "dimensions":
                    data["width"] = 9
                elif kind == "frozen":
                    data["frozen"]["graphicsApi"] = "different"
                elif kind == "cell-list":
                    gap["cellIndices"] = [1, 1]
                elif kind == "malformed":
                    data = []
                old.write_text(json.dumps(data))
                report = module._compare_pair(old, new)
                self.assertFalse(report["pass"])
                self.assertTrue(report["problems"])

    def test_fixed_old_gap_ignores_new_ids_and_excludes_both_gear_masks(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            old, new = make_pair(Path(folder))
            for path, gap_id, gear_pixel in ((old, "old-gap", 0), (new, "new-gap", 1)):
                data = json.loads(path.read_text())
                region = bytes([1] * 4 + [0] * 60)
                seen = bytes(int(i == gear_pixel) for i in range(64))
                def mask(name, value):
                    (path.parent / name).write_bytes(value)
                    return {"path": name, "byteCount": 64, "sha256": hashlib.sha256(value).hexdigest(), "trueCount": sum(value)}
                for view in data["views"]:
                    view["masks"]["gearSeen"] = mask("seen.mask", seen)
                    view["gaps"] = [{"id": gap_id, "cellIndices": [4, 5], "projectedCellRegionMask": mask("region.mask", region), "gearExcludedMask": mask("excluded.mask", bytes([0] * 64))}]
                path.write_text(json.dumps(data))
            report = module._compare_pair(old, new)
            self.assertTrue(report["pass"], report)
            self.assertEqual("old-gap", report["views"][0]["gaps"][0]["id"])
            self.assertEqual([4, 5], report["views"][0]["gaps"][0]["cellIndices"])
            self.assertEqual(2, report["views"][0]["gaps"][0]["commonPixels"])

    def test_six_no_gap_views_keep_calm_checks(self):
        module = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            old, new = make_pair(Path(folder))
            report = module._compare_pair(old, new)
            self.assertTrue(report["pass"], report)
            self.assertEqual(6, len(report["views"]))
            self.assertEqual(12, len(report["checks"]))


def make_pair(root):
    views = [f"{pose}-easy-{hint}" for pose in ("left-cheat", "middle-depth", "right-late-settle") for hint in ("on", "off")]
    paths = []
    for side in ("old", "new"):
        directory = root / side
        directory.mkdir()
        def blob(name, data, true_count=None):
            (directory / name).write_bytes(data)
            entry = {"path": name, "byteCount": len(data), "sha256": hashlib.sha256(data).hexdigest()}
            if true_count is not None:
                entry["trueCount"] = true_count
            return entry
        image = blob("frame.rgba", bytes([128, 128, 128, 255]) * 64)
        mask = blob("full.mask", bytes([1]) * 64, 64)
        manifest = {"schema": "hockey-picture-capture-v1", "width": 8, "height": 8,
                    "sourceCommit": "a" * 40 if side == "old" else "b" * 40,
                    "sourceDiffSha256": "c" * 64, "capturePayloadSha256": "d" * 64,
                    "frozen": {key: "fixture" for key in ("unityVersion", "host", "gpu", "gpuDriver", "graphicsApi", "colorSpace", "qualitySettings", "renderSettings", "crowdDrawn", "poseInputs", "targetFormat", "targetReadWrite", "antiAliasing")},
                    "views": [{"id": view, "camera": {key: "fixture" for key in ("cameraPosition", "cameraRotation", "cameraProjection", "cameraFieldOfView")}, "image": image,
                               "masks": {key: mask for key in ("goalie", "glow", "gearSeen", "calm", "gear")}, "gaps": []} for view in views]}
        path = directory / "manifest.json"
        path.write_text(json.dumps(manifest))
        paths.append(path)
    return paths


if __name__ == "__main__":
    unittest.main()
