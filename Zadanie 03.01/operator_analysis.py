"""Punkt 4: ekstrakcja notatek operatora i jedna analiza LLM."""

from __future__ import annotations

from pathlib import Path
from typing import Dict, List, Optional, Tuple

from analyze_operator_notes import analyze_operator_notes
from extract_operator_notes import extract_operator_notes


def run_operator_analysis(
    files_dir: Optional[Path] = None,
) -> Tuple[Dict[str, str], List[str]]:
    """
    Krok 4:
    1. Deterministycznie tworzy jeden plik: numer pliku + komentarz operatora.
    2. Analizuje notatki LLM-em i zapisuje wynik do niepoprawne_llm.
    3. Zapisuje numery plików zgłoszonych przez operatora do niepoprawne_llm.
    """
    notes = extract_operator_notes(files_dir=files_dir)
    llm_flagged = analyze_operator_notes()
    return notes, llm_flagged


if __name__ == "__main__":
    run_operator_analysis()
