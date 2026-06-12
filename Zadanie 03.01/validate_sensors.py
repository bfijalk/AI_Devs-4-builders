"""Punkt 2: deterministyczna walidacja odczytów czujników."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, List, Optional

from config import FILES_DIR, INVALID_FILES_PATH, SENSOR_RANGES


def is_sensor_reading_invalid(data: Dict[str, Any]) -> bool:
    """Sprawdza zakresy aktywnych czujników i zerowe wartości nieaktywnych."""
    active_sensors = set(data["sensor_type"].split("/"))

    for sensor, (field, min_value, max_value) in SENSOR_RANGES.items():
        value = data[field]
        if sensor in active_sensors:
            if not min_value <= value <= max_value:
                return True
        elif value != 0:
            return True

    return False


def find_invalid_sensor_files(input_dir: Optional[Path] = None) -> List[str]:
    """Przegląda pliki JSON i zwraca nazwy tych z niepoprawnymi odczytami."""
    source_dir = input_dir or FILES_DIR
    invalid_files: List[str] = []

    for path in sorted(source_dir.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        if is_sensor_reading_invalid(data):
            invalid_files.append(path.name)

    return invalid_files


def save_invalid_sensor_files(
    invalid_files: List[str],
    output_path: Optional[Path] = None,
) -> Path:
    """Zapisuje listę niepoprawnych plików do pliku tekstowego."""
    target = output_path or INVALID_FILES_PATH
    target.write_text("\n".join(invalid_files) + ("\n" if invalid_files else ""), encoding="utf-8")
    return target


def validate_sensor_data(
    input_dir: Optional[Path] = None,
    output_path: Optional[Path] = None,
) -> List[str]:
    """Waliduje odczyty czujników i zapisuje niepoprawne pliki do niepoprawne.txt."""
    invalid_files = find_invalid_sensor_files(input_dir)
    target = save_invalid_sensor_files(invalid_files, output_path)

    print(f"[OK]  Found {len(invalid_files)} invalid files")
    print(f"[OK]  Saved to {target}")
    return invalid_files


if __name__ == "__main__":
    validate_sensor_data()
