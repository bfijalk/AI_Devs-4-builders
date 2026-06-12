"""Punkt 3: scalanie zawartości niepoprawnych plików do jednego JSON."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, List, Optional

from config import FILES_DIR, INVALID_CONTENT_PATH, INVALID_FILES_PATH


def load_invalid_filenames(invalid_list_path: Optional[Path] = None) -> List[str]:
    """Wczytuje nazwy niepoprawnych plików z pliku tekstowego."""
    source = invalid_list_path or INVALID_FILES_PATH
    if not source.exists():
        raise FileNotFoundError(f"Nie znaleziono pliku z listą niepoprawnych: {source}")

    return [
        line.strip()
        for line in source.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]


def merge_invalid_sensor_content(
    invalid_list_path: Optional[Path] = None,
    files_dir: Optional[Path] = None,
    output_path: Optional[Path] = None,
) -> Dict[str, Any]:
    """Łączy zawartość plików z niepoprawne.txt w jeden plik niepoprawne_content.json."""
    source_dir = files_dir or FILES_DIR
    target = output_path or INVALID_CONTENT_PATH
    filenames = load_invalid_filenames(invalid_list_path)

    merged: Dict[str, Any] = {}
    for filename in filenames:
        file_path = source_dir / filename
        if not file_path.exists():
            raise FileNotFoundError(f"Nie znaleziono pliku czujnika: {file_path}")

        merged[filename] = json.loads(file_path.read_text(encoding="utf-8"))

    target.write_text(
        json.dumps(merged, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    print(f"[OK]  Merged {len(merged)} files into {target}")
    return merged


if __name__ == "__main__":
    merge_invalid_sensor_content()
