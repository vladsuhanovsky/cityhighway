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
APPID_FILE = Path("steam_appid.txt")
ID_PATTERN = re.compile(r'\bId\s*=\s*"([a-z][a-z0-9_]*)"')
DOC_PATTERN = re.compile(r'^\|\s*`([a-z][a-z0-9_]*)`\s*\|', re.MULTILINE)


def catalog_ids(source):
    section = source.split("IReadOnlyList<Achievement> All =", 1)
    if len(section) != 2:
        raise ValueError("Achievements.All declaration not found")
    section = section[1].split("IReadOnlyList<DailyTask> DailyTasks =", 1)[0]
    return ID_PATTERN.findall(section)


def validate(root, release=False, app_id=None):
    errors = []
    try:
        ids = catalog_ids((root / CATALOG).read_text(encoding="utf-8"))
        listed = DOC_PATTERN.findall((root / PARTNER_DOC).read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        return [str(exc)]
    if not ids:
        errors.append("Achievements.All has no parsed IDs")
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
        if not app_id or not app_id.isdecimal() or int(app_id) in (0, 480):
            errors.append("Release requires --app-id with the real, non-480 Steam AppID")
        try:
            packages = json.loads((root / MANIFEST).read_text(encoding="utf-8"))["dependencies"]
            if "com.rlabrecque.steamworks.net" not in packages:
                errors.append("Steamworks.NET package is absent from Unity manifest")
        except (OSError, ValueError, KeyError, TypeError) as exc:
            errors.append("Cannot validate Unity package manifest: " + str(exc))
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
