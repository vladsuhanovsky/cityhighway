#!/usr/bin/env python3
"""Report untranslated Cyrillic UI literals and validate ru/en catalogs.

This is an approximation: string literals in comments or interpolated strings
may be counted, and content generated without string literals will not appear.
Only catalog problems or missing referenced keys make this script fail.
"""
import argparse
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOC = ROOT / "Assets/CityHighways/Resources/CityHighways/Localization"
UI = ROOT / "Assets/CityHighways/Presentation"
CYRILLIC = re.compile(r"[\u0400-\u04FF]")
STRING = re.compile(r'"((?:\\.|[^"\\])*)"')
REFERENCE = re.compile(r'LocalizationService\.Get\(\s*"([^"]+)"')
FORMAT = re.compile(r"\{(\d+)(?::[^}]*)?\}")


def read_catalog(language, errors):
    path = LOC / (language + ".json")
    data = json.loads(path.read_text(encoding="utf-8"))
    entries = data["entries"]
    keys = [entry["key"] for entry in entries]
    repeated = sorted(key for key, number in Counter(keys).items() if number > 1)
    if repeated:
        errors.append(language + ": duplicate keys: " + ", ".join(repeated))
    for entry in entries:
        if not entry["key"] or not entry["value"]:
            errors.append(language + ": empty key or value")
    return {entry["key"]: entry["value"] for entry in entries}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="artifacts/localization-audit.json")
    args = parser.parse_args()
    errors = []
    ru = read_catalog("ru", errors)
    en = read_catalog("en", errors)
    if set(ru) != set(en):
        errors.append("Catalog keys differ: ru-only=" +
                      str(sorted(set(ru) - set(en))) + "; en-only=" +
                      str(sorted(set(en) - set(ru))))
    for key in set(ru) & set(en):
        if sorted(FORMAT.findall(ru[key])) != sorted(FORMAT.findall(en[key])):
            errors.append("Different formatting placeholders for " + key)

    remaining = []
    references = set()
    for path in sorted(UI.rglob("*.cs")):
        text = path.read_text(encoding="utf-8-sig")
        references.update(REFERENCE.findall(text))
        lines = []
        for index, line in enumerate(text.splitlines(), 1):
            if any(CYRILLIC.search(match.group(1)) for match in STRING.finditer(line)):
                lines.append(index)
        if lines:
            remaining.append({"file": str(path.relative_to(ROOT)).replace("\\", "/"),
                              "literal_lines": len(lines), "line_numbers": lines})

    missing = sorted(references - set(ru))
    if missing:
        errors.append("Referenced keys missing from catalogs: " + ", ".join(missing))
    count = sum(item["literal_lines"] for item in remaining)
    report = {"catalog_keys": len(ru), "referenced_keys": len(references),
              "remaining_cyrillic_literal_lines": count,
              "files": sorted(remaining, key=lambda item: -item["literal_lines"]),
              "validation_errors": errors,
              "note": "Cyrillic string-line scan is approximate; review source context."}
    path = ROOT / args.output
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Localization keys:", len(ru), "UI lines with Cyrillic literals:", count)
    for item in report["files"][:20]:
        print("  ", item["literal_lines"], item["file"])
    if errors:
        for error in errors:
            print("ERROR:", error)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
