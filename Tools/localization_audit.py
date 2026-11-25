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
DIRECT_UI = re.compile(r"""(?:Ui\.(?:Text|Button)|\.tooltip\s*=|\bTitle\s*=)\s*\(?\s*\$?"[^"\n]*[\u0400-\u04FF]""")


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


def dynamic_title_catalog(ru, en, errors):
    """Recognize the actual Achievement/DailyTask initializers, not arbitrary UI text."""
    source = (UI / "Achievements.cs").read_text(encoding="utf-8-sig")
    pattern = re.compile(r'\bnew\s+(Achievement|DailyTask)\s*\{\s*Id\s*=\s*"([^"]+)"')
    covered = {}
    for match in pattern.finditer(source):
        kind, item_id = match.groups()
        prefix = ("achievement." if kind == "Achievement" else "daily.") + item_id
        keys = [prefix + ".title"]
        if kind == "Achievement":
            keys.append(prefix + ".description")
        missing = [key for key in keys if not ru.get(key) or not en.get(key)]
        if missing:
            errors.append("Missing dynamic title keys: " + ", ".join(missing))
        else:
            covered[item_id] = keys
    if not covered:
        errors.append("No catalog-backed achievement or daily title initializers found")
    return covered


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
    # A missing or untranslated English catalog value is always a defect, unlike
    # Russian literals retained in explicit RU branches in C# sources.
    untranslated_catalog = sorted(key for key, value in en.items() if CYRILLIC.search(value))
    if untranslated_catalog:
        errors.append("English entries contain Cyrillic: " + ", ".join(untranslated_catalog))
    for key in set(ru) & set(en):
        if sorted(FORMAT.findall(ru[key])) != sorted(FORMAT.findall(en[key])):
            errors.append("Different formatting placeholders for " + key)

    catalog_backed = dynamic_title_catalog(ru, en, errors)
    remaining = []
    direct_ui = []
    references = set()
    for path in sorted(UI.rglob("*.cs")):
        text = path.read_text(encoding="utf-8-sig")
        references.update(REFERENCE.findall(text))
        lines = []
        for index, line in enumerate(text.splitlines(), 1):
            if any(CYRILLIC.search(match.group(1)) for match in STRING.finditer(line)):
                lines.append(index)
            # Conservative candidate list, not a compiler or a control-flow
            # proof: UI declarations can be followed by an English override.
            if (not line.lstrip().startswith("//") and
                    DIRECT_UI.search(line) and
                    "LocalizationService.Language" not in line and
                    "En ?" not in line and "IsEnglish ?" not in line):
                direct_ui.append({"file": str(path.relative_to(ROOT)).replace("\\", "/"),
                                  "line": index, "source": line.strip()[:220]})
        if lines:
            remaining.append({"file": str(path.relative_to(ROOT)).replace("\\", "/"),
                              "literal_lines": len(lines), "line_numbers": lines})

    # Concatenated keys such as "menu.scenario." are namespace prefixes, not full keys.
    missing = sorted(key for key in references - set(ru) if not (key.endswith(".") and any(actual.startswith(key) for actual in ru)))
    if missing:
        errors.append("Referenced keys missing from catalogs: " + ", ".join(missing))
    # Retain the raw historical candidate count. Classify only known declarations
    # in Achievements.cs whose localized getter keys are present in BOTH catalogs.
    for item in direct_ui:
        match = re.search(r'\bId\s*=\s*"([^"]+)"', item["source"])
        if (item["file"].endswith("/Achievements.cs") and match and
                match.group(1) in catalog_backed):
            item["classification"] = "catalog-backed-fallback"
        else:
            item["classification"] = "needs-context-review"
    unresolved = [item for item in direct_ui if item["classification"] == "needs-context-review"]
    count = sum(item["literal_lines"] for item in remaining)
    report = {"catalog_keys": len(ru), "referenced_keys": len(references),
              "remaining_cyrillic_literal_lines": count,
              "files": sorted(remaining, key=lambda item: -item["literal_lines"]),
              "validation_errors": errors,
              "direct_ui_candidates": direct_ui,
              "direct_ui_candidate_count": len(direct_ui),
              "catalog_backed_title_count": len(catalog_backed),
              "unresolved_direct_ui_candidates": unresolved,
              "unresolved_direct_ui_candidate_count": len(unresolved),
              "note": "Cyrillic string-line scan includes intentional Russian branches. Direct UI candidates are heuristic warnings, not proof of missing translations."}
    path = ROOT / args.output
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Localization keys:", len(ru), "UI lines with Cyrillic literals:", count)
    print("Direct UI translation candidates:", len(direct_ui))
    print("Catalog-backed dynamic titles:", len(catalog_backed))
    print("Candidates needing context review:", len(unresolved))
    for item in unresolved[:20]:
        print("  UI candidate:", item["file"], ":", item["line"])
    for item in report["files"][:20]:
        print("  ", item["literal_lines"], item["file"])
    if errors:
        for error in errors:
            print("ERROR:", error)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
