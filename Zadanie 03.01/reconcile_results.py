"""Punkt 5: analiza wyników LLM i budowa finalnej listy błędnych plików."""

from __future__ import annotations

import json
from pathlib import Path
from typing import List, Optional

from config import FILES_DIR, INVALID_FILES_PATH, INVALID_LLM_PATH, RECHECK_PATH
from validate_sensors import is_sensor_reading_invalid


def load_file_ids(path: Path) -> List[str]:
    if not path.exists():
        raise FileNotFoundError(f"Nie znaleziono pliku: {path}")

    return [
        Path(line.strip()).stem
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]


def save_file_ids(path: Path, file_ids: List[str]) -> Path:
    path.write_text("\n".join(file_ids) + ("\n" if file_ids else ""), encoding="utf-8")
    return path


def find_llm_false_positives(
    llm_flagged_ids: List[str],
    files_dir: Optional[Path] = None,
) -> List[str]:
    """
    Zwraca pliki oznaczone przez operatora (LLM) jako niedziałające,
    ale z poprawnymi danymi wg deterministycznej walidacji z kroku 2.
    """
    source_dir = files_dir or FILES_DIR
    false_positives: List[str] = []

    for file_id in llm_flagged_ids:
        path = source_dir / f"{file_id}.json"
        if not path.exists():
            continue

        data = json.loads(path.read_text(encoding="utf-8"))
        if not is_sensor_reading_invalid(data):
            false_positives.append(file_id)

    return sorted(false_positives)


def analyze_llm_results(
    invalid_files_path: Optional[Path] = None,
    llm_flagged_path: Optional[Path] = None,
    output_path: Optional[Path] = None,
    files_dir: Optional[Path] = None,
) -> List[str]:
    """
    Krok 5:
    1. Bierze wyniki LLM z niepoprawne_llm.
    2. Sprawdza każdy plik deterministyczną metodą z punktu 2.
    3. Jeśli operator uznał plik za niedziałający, a dane są poprawne —
       dodaje go do pliku wynikowego z błędnymi plikami.
    """
    data_invalid_ids = load_file_ids(invalid_files_path or INVALID_FILES_PATH)
    llm_flagged_ids = load_file_ids(llm_flagged_path or INVALID_LLM_PATH)
    operator_false_positives = find_llm_false_positives(llm_flagged_ids, files_dir)

    final_ids = sorted(set(data_invalid_ids) | set(operator_false_positives))
    target = output_path or RECHECK_PATH
    save_file_ids(target, final_ids)

    print(f"[OK]  Pliki z błędnymi danymi (krok 2): {len(data_invalid_ids)}")
    print(f"[OK]  Pliki OK wg danych, ale zgłoszone przez operatora: {len(operator_false_positives)}")
    print(f"[OK]  Finalna lista błędnych plików: {len(final_ids)}")
    print(f"[OK]  Saved to {target}")
    return final_ids


if __name__ == "__main__":
    analyze_llm_results()
