"""Wysyłka finalnej listy recheck do endpointu /verify."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, List, Optional

import requests

from config import HTTP_TIMEOUT, RECHECK_PATH, TASK_NAME, VERIFY_URL, require_api_key


def load_recheck_ids(recheck_path: Optional[Path] = None) -> List[str]:
    """Wczytuje identyfikatory plików do ponownej weryfikacji z recheck.txt."""
    source = recheck_path or RECHECK_PATH
    if not source.exists():
        raise FileNotFoundError(f"Nie znaleziono pliku z listą recheck: {source}")

    return [
        Path(line.strip()).stem
        for line in source.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]


def submit_evaluation(
    recheck_ids: Optional[List[str]] = None,
    recheck_path: Optional[Path] = None,
) -> Dict[str, Any]:
    """Wysyła listę plików do ponownej weryfikacji do Centrali."""
    api_key = require_api_key()
    ids = recheck_ids if recheck_ids is not None else load_recheck_ids(recheck_path)

    payload = {
        "apikey": api_key,
        "task": TASK_NAME,
        "answer": {"recheck": ids},
    }

    print(f"[VERIFY] Wysyłam {len(ids)} identyfikatorów do {VERIFY_URL}")
    response = requests.post(VERIFY_URL, json=payload, timeout=HTTP_TIMEOUT)

    try:
        body = response.json()
    except ValueError:
        body = {"raw": response.text}

    print(f"[VERIFY] Status: {response.status_code}")
    print(json.dumps(body, ensure_ascii=False, indent=2))

    return {
        "success": response.ok,
        "status_code": response.status_code,
        "recheck_count": len(ids),
        "hub_response": body,
    }


if __name__ == "__main__":
    submit_evaluation()
