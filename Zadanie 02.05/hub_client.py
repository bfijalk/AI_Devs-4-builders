"""Komunikacja z endpointem /verify huba."""

from __future__ import annotations

import json
from typing import Any, Dict, List

import requests

from config import API_KEY, HTTP_TIMEOUT, TASK_NAME, VERIFY_URL, require_api_key


def is_hub_success(body: Dict[str, Any], status_ok: bool) -> bool:
    """Hub zwraca flagę w polu flag albo w message jako {FLG:...}."""
    if not status_ok:
        return False
    if body.get("flag"):
        return True
    message = str(body.get("message", ""))
    if body.get("code") == 0 and "{FLG:" in message:
        return True
    return "{FLG:" in json.dumps(body, ensure_ascii=False)


def submit_drone_instructions(instructions: List[str]) -> Dict[str, Any]:
    """Wysyła sekwencję instrukcji drona do endpointu /verify."""
    require_api_key()
    if not instructions:
        raise ValueError("Lista instrukcji nie może być pusta")

    payload = {
        "apikey": API_KEY,
        "task": TASK_NAME,
        "answer": {"instructions": instructions},
    }

    print(f"[VERIFY] Wysyłam {len(instructions)} instrukcji...")
    for index, instruction in enumerate(instructions, 1):
        print(f"  {index:2d}. {instruction}")

    response = requests.post(VERIFY_URL, json=payload, timeout=HTTP_TIMEOUT)
    try:
        body = response.json()
    except ValueError:
        body = {"raw": response.text}

    if is_hub_success(body, response.ok):
        return {
            "success": True,
            "flag": body.get("flag") or body.get("message") or body,
            "hub_response": body,
            "instructions": instructions,
        }

    return {
        "success": False,
        "status_code": response.status_code,
        "hub_feedback": body,
        "instructions": instructions,
        "instruction": (
            "Wysyłka nie powiodła się. Przeanalizuj hub_feedback, popraw sekwencję "
            "instrukcji zgodnie z dokumentacją i spróbuj ponownie."
        ),
    }
