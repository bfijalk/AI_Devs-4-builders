import json
import os
import time

import requests

from config import API_KEY, BOARD_RESET_URL, FILES_DIR, VERIFY_URL


def download_image(url: str, filename: str, *, force: bool = False) -> str:
    """Download *url* to Files/*filename*. Skips download if file already exists
    unless *force* is True (useful when the remote image may have changed)."""
    os.makedirs(FILES_DIR, exist_ok=True)
    filepath = os.path.join(FILES_DIR, filename)

    if not force and os.path.exists(filepath):
        print(f"[CACHE] {filepath} already exists, skipping download")
        return filepath

    print(f"[GET] {url}")
    response = requests.get(url)
    response.raise_for_status()

    with open(filepath, "wb") as f:
        f.write(response.content)
    print(f"[OK]  Saved to {filepath} ({len(response.content)} bytes)")
    return filepath


def reset_board() -> None:
    """Reset the board to its initial state via GET with ?reset=1."""
    print(f"[RESET] {BOARD_RESET_URL}")
    response = requests.get(BOARD_RESET_URL)
    response.raise_for_status()
    print(f"[OK]  Board reset ({len(response.content)} bytes response)")


def send_rotation(cell: str) -> dict:
    """Send a single 90-degree clockwise rotation for *cell* (e.g. '2x3')."""
    payload = {
        "apikey": API_KEY,
        "task": "electricity",
        "answer": {"rotate": cell},
    }
    print(f"[ROTATE] cell {cell}")
    response = requests.post(VERIFY_URL, json=payload)

    try:
        data = response.json()
        print(f"  -> {json.dumps(data, ensure_ascii=False)}")
    except Exception:
        print(f"  -> {response.text}")
        data = {"raw": response.text}
    return data
