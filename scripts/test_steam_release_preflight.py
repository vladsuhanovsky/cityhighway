"""Unit tests for Steam release preflight; run with python -m unittest discover -s scripts -p 'test_*.py'."""
import json
import tempfile
import unittest
from pathlib import Path

from steam_release_preflight import validate


class SteamPreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for name, contents in {
            "Assets/CityHighways/Presentation/Achievements.cs": 'public static readonly IReadOnlyList<Achievement> All = new[] { new Achievement { Id = "first_win" }, new Achievement { Id = "mg_win" } }; public static readonly IReadOnlyList<DailyTask> DailyTasks = new[] {};',
            "Docs/STEAM_ACHIEVEMENT_API_IDS.md": "| API | Group |\n| --- | --- |\n| `first_win` | Classic |\n| `mg_win` | Magnate |\n",
            "Packages/manifest.json": json.dumps({"dependencies": {"com.rlabrecque.steamworks.net": "1.0.0"}}),
            "steam_appid.txt": "123456\n",
        }.items():
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(contents, encoding="utf-8")

    def test_catalog_matches(self):
        self.assertEqual(validate(self.root), [])

    def test_detects_missing_and_extra_ids(self):
        doc = self.root / "Docs/STEAM_ACHIEVEMENT_API_IDS.md"
        doc.write_text("| `first_win` | Classic |\n| `alien` | Wrong |\n", encoding="utf-8")
        report = "\n".join(validate(self.root))
        self.assertIn("mg_win", report)
        self.assertIn("alien", report)

    def test_detects_duplicate_ids(self):
        doc = self.root / "Docs/STEAM_ACHIEVEMENT_API_IDS.md"
        doc.write_text("| `first_win` | A |\n| `first_win` | B |\n| `mg_win` | C |\n", encoding="utf-8")
        self.assertTrue(any("Duplicate" in x for x in validate(self.root)))

    def test_release_rejects_test_appid(self):
        self.assertTrue(any("non-480" in x for x in validate(self.root, release=True, app_id="480")))

    def test_release_rejects_missing_package(self):
        (self.root / "Packages/manifest.json").write_text('{"dependencies":{}}', encoding="utf-8")
        self.assertTrue(any("Steamworks.NET package" in x for x in validate(self.root, release=True, app_id="123456")))

    def test_release_requires_matching_appid_file(self):
        (self.root / "steam_appid.txt").write_text("480\n", encoding="utf-8")
        self.assertTrue(any("steam_appid.txt differs" in x for x in validate(self.root, release=True, app_id="123456")))

    def test_configured_release_passes(self):
        self.assertEqual(validate(self.root, release=True, app_id="123456"), [])


if __name__ == "__main__":
    unittest.main()
