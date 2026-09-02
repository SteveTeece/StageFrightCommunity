#!/usr/bin/env python3
"""Regenerate the `qps-ploc` pseudo-locale resource sets (spec 027, US3 / Decision 9).

For every neutral `.resx` listed below this writes a `<Marker>.qps-ploc.resx` sibling whose
values are pseudo-localised — bracketed and accented so layout/expansion problems and any
un-extracted literal jump out — while every `{Named}` placeholder is preserved verbatim so the
placeholder-parity guard stays green.

`qps-ploc` is a TEST FIXTURE ONLY. `SupportedLanguagesCatalog` excludes every `qps-*` culture
by name, so it never appears in the Settings / Setup language picker or in FR-023 system-language
matching. A handful of keys (OMIT below) are deliberately left out so tests can prove per-key
fallback to en-AU plus the missing-key Warning (SC-003 / SC-004).

Run from the repo root:  python scripts/generate-pseudo-locale.py
"""
from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path
from xml.sax.saxutils import escape

REPO_ROOT = Path(__file__).resolve().parent.parent

NEUTRAL_RESX = [
    "src/StageFright.Core/Modules/Localization/Resources/NavigationResource.resx",
    "src/StageFright.Core/Modules/Localization/Resources/ValidationResource.resx",
    "src/StageFright.Core/Modules/Localization/Resources/EnumsResource.resx",
    "src/StageFright.Reports/Resources/ReportsResource.resx",
    "src/StageFright.UI/Resources/Strings/SharedResource.resx",
    "src/StageFright.UI/Resources/Strings/DashboardResource.resx",
    "src/StageFright.UI/Resources/Strings/MembersResource.resx",
    "src/StageFright.UI/Resources/Strings/RehearsalsResource.resx",
    "src/StageFright.UI/Resources/Strings/EventsResource.resx",
    "src/StageFright.UI/Resources/Strings/FinanceResource.resx",
    "src/StageFright.UI/Resources/Strings/SettingsResource.resx",
    "src/StageFright.UI/Resources/Strings/SetupResource.resx",
]

# Keys deliberately left untranslated so `qps-ploc` falls back to en-AU for them (SC-004).
OMIT = {
    "SharedResource.resx": {"Shared_Action_Cancel"},
    "NavigationResource.resx": {"Nav_Sidebar_BrandText"},
    "EnumsResource.resx": {"Enum_Theme_Light"},
}

RESHEADERS = [
    ("resmimetype", "text/microsoft-resx"),
    ("version", "2.0"),
    ("reader", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
    ("writer", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
]

PLACEHOLDER = re.compile(r"\{[A-Za-z][A-Za-z0-9]*\}")
ACCENT = str.maketrans("aeiouAEIOUcnyCNY", "áéíóúÁÉÍÓÚçñýÇÑÝ")


def pseudo(value: str) -> str:
    """Pseudo-localise one (already-decoded) value, keeping {Placeholders} verbatim."""
    parts = PLACEHOLDER.split(value)
    tokens = PLACEHOLDER.findall(value)
    rebuilt = []
    for i, part in enumerate(parts):
        rebuilt.append(part.translate(ACCENT))
        if i < len(tokens):
            rebuilt.append(tokens[i])
    return "⟦" + "".join(rebuilt) + "—⟧"


def transform_file(neutral_path: Path) -> Path:
    root = ET.parse(neutral_path).getroot()
    omit = OMIT.get(neutral_path.name, set())

    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<root>"]
    for name, val in RESHEADERS:
        lines.append(f'  <resheader name="{name}"><value>{escape(val)}</value></resheader>')

    for data in root.findall("data"):
        name = data.get("name")
        if not name or name in omit:
            continue
        value = (data.findtext("value") or "")
        comment = data.findtext("comment")
        comment_xml = f"<comment>{escape(comment)}</comment>" if comment else ""
        lines.append(
            f'  <data name="{escape(name)}" xml:space="preserve">'
            f"<value>{escape(pseudo(value))}</value>{comment_xml}</data>"
        )

    lines.append("</root>")
    target = neutral_path.with_name(neutral_path.stem + ".qps-ploc.resx")
    target.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return target


def main() -> None:
    for rel in NEUTRAL_RESX:
        neutral = REPO_ROOT / rel
        if not neutral.is_file():
            raise SystemExit(f"missing neutral resx: {rel}")
        written = transform_file(neutral)
        print(f"wrote {written.relative_to(REPO_ROOT).as_posix()}")


if __name__ == "__main__":
    main()
