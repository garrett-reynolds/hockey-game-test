"""Offline process checks for the frozen manifest-to-report command."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from test_picture_pair_compare import make_pair

TOOL = Path(__file__).parents[1] / "picture_pair_compare.py"


class PairCommandTests(unittest.TestCase):
    def test_valid_small_pair_emits_success_report_through_real_command_main(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            old, new = make_pair(root)
            report = root / "report.json"
            before = {path: path.read_bytes() for side in (old.parent, new.parent) for path in side.iterdir()}
            # Existing tiny fixtures use the real engine. Keep the production
            # dimensions/source gate separately covered; add no CLI bypass flag.
            entry = (
                "import importlib.util,sys; "
                "spec=importlib.util.spec_from_file_location('pair_command',sys.argv[1]); "
                "module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module); "
                "module.compare_pair=module._compare_pair; "
                "raise SystemExit(module.main(sys.argv[2:]))"
            )
            result = subprocess.run(
                [sys.executable, "-c", entry, str(TOOL), str(old), str(new), "--output", str(report)],
                capture_output=True, text=True, timeout=10,
            )
            self.assertEqual(0, result.returncode, result.stderr)
            saved = json.loads(report.read_text())
            self.assertTrue(saved["pass"])
            self.assertEqual([], saved["problems"])
            self.assertEqual(6, len(saved["views"]))
            self.assertEqual(12, len(saved["checks"]))
            self.assertTrue(all(not check["fails"] for check in saved["checks"]))
            self.assertEqual(saved, json.loads(result.stdout))
            self.assertEqual("a" * 40, saved["sources"]["old"]["sourceCommit"])
            self.assertEqual(before, {path: path.read_bytes() for path in before})

    def test_bad_pair_returns_failed_json_without_changing_inputs(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            old, new, report = (root / name for name in ("old.json", "new.json", "report.json"))
            baseline = b'{"width":8,"height":8}\n'
            old.write_bytes(baseline)
            new.write_bytes(baseline)
            result = subprocess.run(
                [sys.executable, str(TOOL), str(old), str(new), "--output", str(report)],
                capture_output=True, text=True, timeout=10,
            )
            self.assertEqual(1, result.returncode, result.stderr)
            saved = json.loads(report.read_text())
            self.assertFalse(saved["pass"])
            self.assertEqual(["production dimensions must be 1280x720"], saved["problems"])
            self.assertEqual(saved, json.loads(result.stdout))
            self.assertEqual(baseline, old.read_bytes())
            self.assertEqual(baseline, new.read_bytes())

    def test_existing_report_is_never_replaced(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            old, new, report = (root / name for name in ("old.json", "new.json", "report.json"))
            for path in (old, new):
                path.write_text('{"width":8,"height":8}')
            original_report = b"keep the prior proof\n"
            report.write_bytes(original_report)
            result = subprocess.run(
                [sys.executable, str(TOOL), str(old), str(new), "--output", str(report)],
                capture_output=True, text=True, timeout=10,
            )
            self.assertEqual(2, result.returncode)
            self.assertEqual(original_report, report.read_bytes())
            self.assertEqual("", result.stdout)


if __name__ == "__main__":
    unittest.main()
