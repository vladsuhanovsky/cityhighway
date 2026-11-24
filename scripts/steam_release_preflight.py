#!/usr/bin/env python3
"""Static Steam achievement release preflight (no Unity or Steam SDK required)."""
import argparse
import json
import re
import sys
from pathlib import Path

CATALOG = Path("Assets/CityHighways/Presentation/Achievements.cs")
PARTNER_DOC = Path("Docs/STEAM_ACHIEVEMENT_API_IDS.md")
MANIFEST = Path("Packages/manifest.json")
LOCKFILE = Path("Packages/packages-lock.json")
STEAM_ASMDEF = Path("Assets/CityHighways/Steam/CityHighways.Steam.asmdef")
STEAM_SOURCE = Path("Assets/CityHighways/Steam/SteamPlatform.cs")
STEAM_PACKAGE = "com.rlabrecque.steamworks.net"
APPID_FILE = Path("steam_appid.txt")
# Parse all quoted Id values first; do not silently skip an invalid
# uppercase/noncanonical ID or a Markdown API row with an invalid name.
ID_PATTERN = re.compile(r'\bId\s*=\s*"([^"]*)"')
DOC_PATTERN = re.compile(r'^\|\s*`([^`]+)`\s*\|', re.MULTILINE)
VALID_ID = re.compile(r'[a-z][a-z0-9_]*\Z')


def catalog_ids(source):
    section = source.split("IReadOnlyList<Achievement> All =", 1)
    if len(section) != 2:
        raise ValueError("Achievements.All declaration not found")
    section = section[1].split("IReadOnlyList<DailyTask> DailyTasks =", 1)[0]
    ids = ID_PATTERN.findall(section)
    invalid = [value for value in ids if VALID_ID.fullmatch(value) is None]
    if invalid:
        raise ValueError("Invalid achievement API IDs: " + ", ".join(invalid))
    return ids


def valid_release_app_id(value):
    """A canonical, non-test 32-bit Steam AppID (decimal ASCII only)."""
    return (isinstance(value, str) and
            re.fullmatch(r"[1-9][0-9]{0,9}", value) is not None and
            0 < int(value) <= 4294967295 and int(value) != 480)


def validate(root, release=False, app_id=None):
    errors = []
    try:
        ids = catalog_ids((root / CATALOG).read_text(encoding="utf-8"))
        listed = DOC_PATTERN.findall((root / PARTNER_DOC).read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        return [str(exc)]
    if not ids:
        errors.append("Achievements.All has no parsed IDs")
    invalid_doc = [value for value in listed if VALID_ID.fullmatch(value) is None]
    if invalid_doc:
        errors.append("Invalid Steamworks API IDs in table: " +
                      ", ".join(sorted(set(invalid_doc))))
    for name, values in (("catalog", ids), ("Steamworks API table", listed)):
        duplicates = sorted({value for value in values if values.count(value) > 1})
        if duplicates:
            errors.append(f"Duplicate IDs in {name}: {', '.join(duplicates)}")
    missing = sorted(set(ids) - set(listed))
    extra = sorted(set(listed) - set(ids))
    if missing:
        errors.append("Missing Steamworks API IDs: " + ", ".join(missing))
    if extra:
        errors.append("Unknown Steamworks API IDs: " + ", ".join(extra))
    if release:
        if not valid_release_app_id(app_id):
            errors.append("Release requires --app-id with the real, non-480 Steam AppID")
        try:
            packages = json.loads((root / MANIFEST).read_text(encoding="utf-8"))["dependencies"]
            if not isinstance(packages, dict):
                raise ValueError("dependencies must be an object")
            package_ref = packages.get(STEAM_PACKAGE)
            if not isinstance(package_ref, str) or not package_ref.strip():
                errors.append("Steamworks.NET package is absent or has no version in Unity manifest")
        except (OSError, ValueError, KeyError, TypeError) as exc:
            errors.append("Cannot validate Unity package manifest: " + str(exc))
        try:
            lock = json.loads((root / LOCKFILE).read_text(encoding="utf-8"))
            entries = lock["dependencies"]
            if not isinstance(entries, dict):
                raise ValueError("dependencies must be an object")
            resolved = entries.get(STEAM_PACKAGE)
            if (not isinstance(resolved, dict) or
                    not isinstance(resolved.get("version"), str) or
                    not resolved["version"].strip() or
                    resolved.get("depth") != 0):
                errors.append("Steamworks.NET package is missing or unresolved as a direct dependency in Unity packages-lock.json")
        except (OSError, ValueError, KeyError, TypeError) as exc:
            errors.append("Cannot validate Unity packages-lock.json: " + str(exc))

        try:
            assembly = json.loads((root / STEAM_ASMDEF).read_text(encoding="utf-8"))
            references = assembly["references"]
            constraints = assembly["defineConstraints"]
            versions = assembly["versionDefines"]
            if (not isinstance(references, list) or
                    STEAM_PACKAGE not in references or
                    "CityHighways.Net" not in references or
                    not isinstance(constraints, list) or
                    "STEAMWORKS_NET" not in constraints or
                    not isinstance(versions, list) or
                    not any(isinstance(item, dict) and
                            item.get("name") == STEAM_PACKAGE and
                            item.get("define") == "STEAMWORKS_NET"
                            for item in versions)):
                errors.append("Steam assembly definition must reference Steamworks.NET and use its STEAMWORKS_NET version define")
        except (OSError, ValueError, KeyError, TypeError) as exc:
            errors.append("Cannot validate optional Steam assembly definition: " + str(exc))

        try:
            native_source = (root / STEAM_SOURCE).read_text(encoding="utf-8")
            if not native_source.lstrip().startswith("#if STEAMWORKS_NET"):
                errors.append("Steam native platform must be guarded by #if STEAMWORKS_NET")
        except OSError as exc:
            errors.append("Cannot validate Steam native platform source: " + str(exc))

        if (root / APPID_FILE).exists():
            value = (root / APPID_FILE).read_text(encoding="utf-8").strip()
            if not app_id or value != app_id:
                errors.append("steam_appid.txt differs from --app-id; remove development ID or configure the real one")
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--release", action="store_true", help="also enforce release Steam package/AppID safeguards")
    parser.add_argument("--app-id", help="real Steamworks AppID (required for --release)")
    args = parser.parse_args(argv)
    errors = validate(args.root, args.release, args.app_id)
    if errors:
        for error in errors:
            print("FAIL: " + error, file=sys.stderr)
        return 1
    print("PASS: Steam achievement catalog matches Partner API ID table" + ("; release configuration checked" if args.release else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
