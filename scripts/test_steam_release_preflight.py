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
            "Packages/packages-lock.json": json.dumps({"dependencies": {
                "com.rlabrecque.steamworks.net": {"version": "1.0.0", "depth": 0, "source": "registry"}
            }}),
            "Assets/CityHighways/Steam/CityHighways.Steam.asmdef": json.dumps({
                "name": "CityHighways.Steam",
                "references": ["CityHighways.Net", "com.rlabrecque.steamworks.net"],
                "defineConstraints": ["STEAMWORKS_NET"],
                "versionDefines": [{
                    "name": "com.rlabrecque.steamworks.net",
                    "expression": "",
                    "define": "STEAMWORKS_NET"
                }]
            }),
            "Assets/CityHighways/Steam/SteamPlatform.cs": "#if STEAMWORKS_NET\n#endif\n",
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

    def test_rejects_uppercase_or_noncanonical_catalog_id(self):
        catalog = self.root / "Assets/CityHighways/Presentation/Achievements.cs"
        catalog.write_text(catalog.read_text(encoding="utf-8").replace(
            'Id = "mg_win"', 'Id = "MG_WIN"'), encoding="utf-8")
        self.assertTrue(any("Invalid achievement API IDs" in x
                            for x in validate(self.root)))

    def test_rejects_noncanonical_partner_table_id(self):
        doc = self.root / "Docs/STEAM_ACHIEVEMENT_API_IDS.md"
        doc.write_text(doc.read_text(encoding="utf-8").replace(
            "`mg_win`", "`MG_WIN`"), encoding="utf-8")
        report = "\n".join(validate(self.root))
        self.assertIn("Invalid Steamworks API IDs", report)
        self.assertIn("MG_WIN", report)

    def test_daily_task_ids_are_not_counted_as_achievements(self):
        catalog = self.root / "Assets/CityHighways/Presentation/Achievements.cs"
        catalog.write_text(catalog.read_text(encoding="utf-8") +
            ' new DailyTask { Id = "daily_task_only" };', encoding="utf-8")
        self.assertEqual(validate(self.root), [])

    def test_release_rejects_test_appid(self):
        self.assertTrue(any("non-480" in x for x in validate(self.root, release=True, app_id="480")))

    def test_release_rejects_invalid_app_id_forms(self):
        invalid = [None, "", "0", "480", "0480", "00123456",
                   "-123456", " 123456", "123456 ", "1.0",
                   "１２３４５６", "4294967296", "99999999999"]
        for app_id in invalid:
            with self.subTest(app_id=app_id):
                self.assertTrue(any("real, non-480 Steam AppID" in x
                                    for x in validate(self.root, release=True,
                                                      app_id=app_id)))

    def test_release_accepts_highest_uint32_app_id(self):
        (self.root / "steam_appid.txt").write_text("4294967295\n", encoding="utf-8")
        self.assertEqual(validate(self.root, release=True, app_id="4294967295"), [])

    def test_release_allows_appid_file_to_be_absent_in_packaged_build(self):
        (self.root / "steam_appid.txt").unlink()
        self.assertEqual(validate(self.root, release=True, app_id="123456"), [])

    def test_release_rejects_missing_package(self):
        (self.root / "Packages/manifest.json").write_text('{"dependencies":{}}', encoding="utf-8")
        self.assertTrue(any("Steamworks.NET package" in x for x in validate(self.root, release=True, app_id="123456")))

    def test_release_rejects_blank_manifest_package_version(self):
        manifest = self.root / "Packages/manifest.json"
        for value in ["", "   ", None]:
            with self.subTest(value=value):
                manifest.write_text(json.dumps({"dependencies": {
                    "com.rlabrecque.steamworks.net": value}}), encoding="utf-8")
                self.assertTrue(any("Steamworks.NET package" in x
                                    for x in validate(self.root, release=True,
                                                      app_id="123456")))

    def test_release_rejects_unresolved_or_transitive_lockfile_package(self):
        lock = self.root / "Packages/packages-lock.json"
        for dependencies in [
            {},
            {"com.rlabrecque.steamworks.net": {"version": "", "depth": 0}},
            {"com.rlabrecque.steamworks.net": {"version": "1.0.0", "depth": 1}},
        ]:
            with self.subTest(dependencies=dependencies):
                lock.write_text(json.dumps({"dependencies": dependencies}),
                                encoding="utf-8")
                self.assertTrue(any("Steamworks.NET package is missing or unresolved" in x
                                    for x in validate(self.root, release=True,
                                                      app_id="123456")))

    def test_release_rejects_corrupt_unity_package_lock(self):
        (self.root / "Packages/packages-lock.json").write_text(
            "{bad json", encoding="utf-8")
        self.assertTrue(any("Cannot validate Unity packages-lock.json" in x
                            for x in validate(self.root, release=True,
                                              app_id="123456")))

    def test_release_requires_matching_appid_file(self):
        (self.root / "steam_appid.txt").write_text("480\n", encoding="utf-8")
        self.assertTrue(any("steam_appid.txt differs" in x for x in validate(self.root, release=True, app_id="123456")))

    def test_release_rejects_broken_optional_steam_assembly(self):
        asmdef = self.root / "Assets/CityHighways/Steam/CityHighways.Steam.asmdef"
        for settings in [
            {"references": ["CityHighways.Net"], "defineConstraints": ["STEAMWORKS_NET"],
             "versionDefines": []},
            {"references": ["CityHighways.Net", "com.rlabrecque.steamworks.net"],
             "defineConstraints": [], "versionDefines": []},
            {"references": ["CityHighways.Net", "com.rlabrecque.steamworks.net"],
             "defineConstraints": ["STEAMWORKS_NET"],
             "versionDefines": [{"name": "com.other", "define": "STEAMWORKS_NET"}]}
        ]:
            with self.subTest(settings=settings):
                asmdef.write_text(json.dumps(settings), encoding="utf-8")
                self.assertTrue(any("Steam assembly definition" in x
                                    for x in validate(self.root, release=True,
                                                      app_id="123456")))

    def test_release_rejects_unguarded_native_steam_platform(self):
        source = self.root / "Assets/CityHighways/Steam/SteamPlatform.cs"
        source.write_text("public class SteamPlatform {}\n", encoding="utf-8")
        self.assertTrue(any("guarded by #if STEAMWORKS_NET" in x
                            for x in validate(self.root, release=True,
                                              app_id="123456")))

    def test_release_rejects_missing_native_steam_source(self):
        (self.root / "Assets/CityHighways/Steam/SteamPlatform.cs").unlink()
        self.assertTrue(any("Cannot validate Steam native platform source" in x
                            for x in validate(self.root, release=True,
                                              app_id="123456")))

    def test_normal_catalog_audit_remains_independent_of_steam_sdk(self):
        (self.root / "Packages/packages-lock.json").unlink()
        (self.root / "Assets/CityHighways/Steam/SteamPlatform.cs").unlink()
        (self.root / "Assets/CityHighways/Steam/CityHighways.Steam.asmdef").unlink()
        self.assertEqual(validate(self.root), [])

    def test_configured_release_passes(self):
        self.assertEqual(validate(self.root, release=True, app_id="123456"), [])


if __name__ == "__main__":
    unittest.main()
