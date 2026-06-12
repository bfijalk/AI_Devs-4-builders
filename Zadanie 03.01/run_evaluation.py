"""Uruchamia całe flow zadania evaluation (bez ponownego pobierania danych)."""

from __future__ import annotations

from typing import Any, Dict

from merge_invalid_content import merge_invalid_sensor_content
from operator_analysis import run_operator_analysis
from reconcile_results import analyze_llm_results
from submit_evaluation import submit_evaluation
from validate_sensors import validate_sensor_data


def run_evaluation() -> Dict[str, Any]:
    """Uruchamia kroki 2–5 z task.md i wysyła odpowiedź do Centrali."""
    print("[1/5] Walidacja danych pomiarowych (krok 2)...")
    invalid_files = validate_sensor_data()

    print("\n[2/5] Scalanie niepoprawnych plików (krok 3)...")
    merged_content = merge_invalid_sensor_content()

    print("\n[3/5] Analiza notatek operatora (krok 4)...")
    operator_notes, llm_flagged = run_operator_analysis()

    print("\n[4/5] Analiza wyników LLM (krok 5)...")
    recheck_ids = analyze_llm_results()

    print("\n[5/5] Wysyłka odpowiedzi do /verify...")
    submission = submit_evaluation(recheck_ids=recheck_ids)

    return {
        "invalid_files": invalid_files,
        "merged_count": len(merged_content),
        "operator_notes_count": len(operator_notes),
        "llm_flagged_count": len(llm_flagged),
        "recheck_ids": recheck_ids,
        "submission": submission,
    }


if __name__ == "__main__":
    result = run_evaluation()
    if result["submission"].get("success"):
        print("\n[OK]  Zadanie zakończone sukcesem.")
    else:
        print("\n[!]   Wysyłka nie powiodła się — sprawdź odpowiedź huba.")
