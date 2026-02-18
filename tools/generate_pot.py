#!/usr/bin/env python
"""Generate POT entries from JSON definition files.

Reads item, skill, and entity definition JSONs and outputs PO-format entries
for Name and Description fields. Merge the output into locale/messages.pot.

Usage:
    python tools/generate_pot.py > locale/json_strings.pot
    # Then merge with Godot-extracted POT if needed
"""

import json
import os
import sys

# Ensure stdout uses UTF-8 encoding (Windows defaults to cp1252/other locale encodings)
if sys.stdout.encoding != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# (directory, key_prefix, has_description)
SOURCES = [
    ("resources/items", "item", True),
    ("resources/skills", "skill", True),
    ("resources/entities/definitions", "entity", True),
]


def collect_entries():
    entries = []
    for rel_dir, prefix, has_desc in SOURCES:
        abs_dir = os.path.join(PROJECT_ROOT, rel_dir)
        if not os.path.isdir(abs_dir):
            print(f"# Warning: directory not found: {rel_dir}", file=sys.stderr)
            continue
        for fname in sorted(os.listdir(abs_dir)):
            if not fname.endswith(".json"):
                continue
            fpath = os.path.join(abs_dir, fname)
            with open(fpath, "r", encoding="utf-8") as f:
                try:
                    data = json.load(f)
                except json.JSONDecodeError:
                    print(f"# Warning: invalid JSON: {fpath}", file=sys.stderr)
                    continue

            item_id = data.get("Id")
            name = data.get("Name")
            if not item_id or not name:
                continue

            key_upper = item_id.upper()
            # Name entry
            entries.append((f"{prefix}.name.{key_upper}", name, f"{rel_dir}/{fname}"))
            # Description entry
            desc = data.get("Description")
            if has_desc and desc:
                entries.append((f"{prefix}.desc.{key_upper}", desc, f"{rel_dir}/{fname}"))
    return entries


def write_pot(entries):
    print("# Auto-generated from JSON definitions")
    print("# Do not edit manually. Re-run tools/generate_pot.py to update.")
    print("#")
    print('msgid ""')
    print('msgstr ""')
    print('"Project-Id-Version: Veil of Ages\\n"')
    print('"MIME-Version: 1.0\\n"')
    print('"Content-Type: text/plain; charset=UTF-8\\n"')
    print('"Content-Transfer-Encoding: 8bit\\n"')
    print()
    for key, value, source in entries:
        print(f"#: {source}")
        print(f'msgid "{key}"')
        print(f'msgstr "{value}"')
        print()


if __name__ == "__main__":
    entries = collect_entries()
    write_pot(entries)
