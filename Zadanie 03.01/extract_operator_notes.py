"""Punkt 4 (część 1): deterministyczne zestawienie notatek operatora."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Dict, Optional

from config import FILES_DIR, OPERATOR_NOTES_PATH


def extract_operator_notes(
    files_dir: Optional[Path] = None,
    output_path: Optional[Path] = None,
) -> Dict[str, str]:
    """Zbiera numer pliku i komentarz operatora ze wszystkich plików JSON."""
    source_dir = files_dir or FILES_DIR
    target = output_path or OPERATOR_NOTES_PATH

    notes: Dict[str, str] = {}
    for path in sorted(source_dir.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        notes[path.stem] = data["operator_notes"]

    target.write_text(json.dumps(notes, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"[OK]  Extracted {len(notes)} operator notes to {target}")
    return notes


if __name__ == "__main__":
    extract_operator_notes()
