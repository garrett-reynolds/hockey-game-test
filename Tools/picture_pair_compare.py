"""Compare saved same-frame captures without Unity. No third-party packages."""
import hashlib
import json
import math
from pathlib import Path
import re
import struct

VIEWS = tuple(f"{pose}-easy-{hint}" for pose in ("left-cheat", "middle-depth", "right-late-settle") for hint in ("on", "off"))
MASKS = ("goalie", "glow", "gearSeen", "calm", "gear")
FROZEN = ("unityVersion", "host", "gpu", "gpuDriver", "graphicsApi", "colorSpace", "qualitySettings", "renderSettings", "crowdDrawn", "poseInputs", "targetFormat", "targetReadWrite", "antiAliasing")
CAMERA = ("cameraPosition", "cameraRotation", "cameraProjection", "cameraFieldOfView")


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _float(value):
    """Match the C# float operations used by PictureMeasures."""
    return struct.unpack("f", struct.pack("f", value))[0]


def _luma(data):
    result = []
    for i in range(0, len(data), 4):
        r = _float(_float(0.2126) * data[i])
        g = _float(_float(0.7152) * data[i + 1])
        b = _float(_float(0.0722) * data[i + 2])
        result.append(_float(_float(_float(r + g) + b) / 255))
    return result


def _read(path):
    manifest = json.loads(path.read_text(encoding="utf-8-sig"))
    _require(manifest["schema"] == "hockey-picture-capture-v1", "wrong manifest schema")
    w, h = manifest["width"], manifest["height"]
    _require(type(w) is int and type(h) is int and w >= 3 and h >= 3, "wrong dimensions")
    for field, length in (("sourceCommit", 40), ("sourceDiffSha256", 64), ("capturePayloadSha256", 64)):
        _require(re.fullmatch(f"[0-9a-fA-F]{{{length}}}", manifest[field]) is not None, "bad " + field)
    for field in FROZEN:
        _require(field in manifest["frozen"] and manifest["frozen"][field] is not None, "missing frozen " + field)
    views = manifest["views"]
    _require(len(views) == 6 and {v["id"] for v in views} == set(VIEWS), "missing or duplicate view")
    def blob(entry, count, mask=False):
        target = (path.parent / entry["path"]).resolve()
        _require(target.is_relative_to(path.parent.resolve()), "file escapes manifest folder")
        data = target.read_bytes()
        _require(len(data) == count == entry["byteCount"], "wrong byte count")
        _require(hashlib.sha256(data).hexdigest() == entry["sha256"], "bad file hash")
        if mask:
            _require(all(v in (0, 1) for v in data), "non-binary mask byte")
            _require(sum(data) == entry["trueCount"], "wrong mask true count")
        return data
    for view in views:
        for field in CAMERA:
            _require(field in view["camera"] and view["camera"][field] is not None, "missing camera " + field)
        view["_luma"] = _luma(blob(view["image"], w * h * 4))
        view["_masks"] = {key: blob(view["masks"][key], w * h, True) for key in MASKS}
        _require(isinstance(view["gaps"], list), "missing Gap list")
        ids = set()
        for gap in view["gaps"]:
            _require(isinstance(gap["id"], str) and gap["id"] and gap["id"] not in ids, "bad or duplicate Gap id")
            ids.add(gap["id"])
            cells = gap["cellIndices"]
            _require(bool(cells) and all(type(c) is int and c >= 0 for c in cells) and cells == sorted(set(cells)), "bad Gap cell list")
            gap["_region"] = blob(gap["projectedCellRegionMask"], w * h, True)
            gap["_excluded"] = blob(gap["gearExcludedMask"], w * h, True)
    return manifest


def _calm(luma, mask, w, h):
    values = [value for value, enabled in zip(luma, mask) if enabled]
    _require(bool(values), "empty common Calm")
    mean = sum(values) / len(values)
    contrast = math.sqrt(max(0, sum(v * v for v in values) / len(values) - mean * mean))
    inner = edges = 0
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            ids = [(y + dy) * w + x + dx for dy in (-1, 0, 1) for dx in (-1, 0, 1)]
            if not all(mask[i] for i in ids):
                continue
            inner += 1
            a, b, c, d, _, f, g, j, k = [luma[i] for i in ids]
            gx = _float(_float(_float(c + _float(2 * f)) + k) - _float(_float(a + _float(2 * d)) + g))
            gy = _float(_float(_float(g + _float(2 * j)) + k) - _float(_float(a + _float(2 * b)) + c))
            if math.sqrt(_float(_float(gx * gx) + _float(gy * gy))) / 4 > _float(0.1):
                edges += 1
    _require(inner > 0, "empty common Calm interior")
    return {"contrast": contrast, "edges": edges / inner, "pixels": len(values), "interiorPixels": inner}


def _check(view, metric, before, now, lower=False):
    _require(math.isfinite(before) and math.isfinite(now), "non-finite measure")
    worse = before - now if lower else now - before
    return {"view": view, "metric": metric, "old": before, "new": now,
            "worsePercent": 100 * worse / abs(before) if before else None,
            "fails": worse > 0 if before == 0 else 100 * worse / abs(before) > 10 + 1e-9}


def _standout(luma, gap, gear):
    gap_values = [v for v, enabled in zip(luma, gap) if enabled]
    gear_values = [v for v, enabled in zip(luma, gear) if enabled]
    _require(bool(gap_values), "empty fixed Gap pixels")
    _require(bool(gear_values), "empty common gear pixels")
    return abs(sum(gap_values) / len(gap_values) - sum(gear_values) / len(gear_values))


def compare_pair(old_manifest_path, new_manifest_path):
    """Compare the frozen production pair. Small fixture bypasses stay private."""
    try:
        old = json.loads(Path(old_manifest_path).read_text(encoding="utf-8-sig"))
        new = json.loads(Path(new_manifest_path).read_text(encoding="utf-8-sig"))
        _require(all((m["width"], m["height"]) == (1280, 720) for m in (old, new)), "production dimensions must be 1280x720")
        _require(old["sourceCommit"] == "0a31aeff3ef4a2509a74f672105dda8575ee043a" and new["sourceCommit"] == "48bfa52e2205f8fc97ee77d458c97d991cba3689", "source commits differ from frozen pair contract")
    except (KeyError, ValueError, TypeError, OSError) as error:
        return {"schema": "hockey-picture-common-region-v1", "pass": False, "problems": [str(error)], "views": [], "checks": []}
    return _compare_pair(old_manifest_path, new_manifest_path)


def _compare_pair(old_manifest_path, new_manifest_path):
    """Private whole-job engine for small pure fixtures and the public wrapper."""
    report = {"schema": "hockey-picture-common-region-v1", "pass": False, "problems": [], "views": [], "checks": []}
    try:
        old, new = _read(Path(old_manifest_path)), _read(Path(new_manifest_path))
        _require((old["width"], old["height"]) == (new["width"], new["height"]), "different dimensions")
        _require(old["frozen"] == new["frozen"], "different frozen render inputs")
        _require(old["capturePayloadSha256"] == new["capturePayloadSha256"], "different capture payload")
        report["sources"] = {side: {key: manifest[key] for key in ("sourceCommit", "sourceDiffSha256", "capturePayloadSha256")} for side, manifest in (("old", old), ("new", new))}
        for before in old["views"]:
            now = next(v for v in new["views"] if v["id"] == before["id"])
            _require(before["camera"] == now["camera"], "different camera")
            mask = bytes(a & b for a, b in zip(before["_masks"]["calm"], now["_masks"]["calm"]))
            a = _calm(before["_luma"], mask, old["width"], old["height"])
            b = _calm(now["_luma"], mask, old["width"], old["height"])
            result_view = {"id": before["id"], "commonCalm": {"old": a, "new": b}, "gaps": [],
                           "rawMaskCounts": {"old": {k: sum(v) for k, v in before["_masks"].items()}, "new": {k: sum(v) for k, v in now["_masks"].items()}}}
            report["views"].append(result_view)
            result_view["sourceMeasures"] = {}
            for side, view in (("old", before), ("new", now)):
                own = {"calm": _calm(view["_luma"], view["_masks"]["calm"], old["width"], old["height"]), "gaps": []}
                for own_gap in view["gaps"]:
                    # Old reports can contain empty source samples. Keep that
                    # evidence explicitly; only common samples decide the pair.
                    gap_count = sum(own_gap["_excluded"])
                    gear_count = sum(view["_masks"]["gear"])
                    own["gaps"].append({"id": own_gap["id"], "cellIndices": own_gap["cellIndices"],
                                        "pixels": gap_count, "gearPixels": gear_count,
                                        "standOut": _standout(view["_luma"], own_gap["_excluded"], view["_masks"]["gear"]) if gap_count and gear_count else None})
                result_view["sourceMeasures"][side] = own
            for metric in ("contrast", "edges"):
                report["checks"].append(_check(before["id"], metric, a[metric], b[metric]))
            gear = bytes(a & b for a, b in zip(before["_masks"]["gear"], now["_masks"]["gear"]))
            for gap in before["gaps"]:
                fixed = bytes(region and not (old_seen or new_seen) for region, old_seen, new_seen in zip(gap["_region"], before["_masks"]["gearSeen"], now["_masks"]["gearSeen"]))
                old_value = _standout(before["_luma"], fixed, gear)
                new_value = _standout(now["_luma"], fixed, gear)
                result_view["gaps"].append({"id": gap["id"], "cellIndices": gap["cellIndices"], "old": old_value, "new": new_value,
                                           "commonPixels": sum(fixed), "commonGearPixels": sum(gear), "oldRegionPixels": sum(gap["_region"]), "oldGearExcludedPixels": sum(gap["_excluded"])})
                report["checks"].append(_check(before["id"], "gapStandOut:" + gap["id"], old_value, new_value, lower=True))
        report["pass"] = not any(check["fails"] for check in report["checks"])
    except (KeyError, ValueError, TypeError, OSError, StopIteration) as error:
        report["problems"].append(str(error))
    return report
