"""Pobieranie artefaktów zadania drone."""

from __future__ import annotations

from pathlib import Path
from typing import Dict, Optional

from config import DRONE_DOC_URL, FILES_DIR, drone_map_url, require_api_key
from io_utils import download_file


def download_drone_artifacts(
    api_key: Optional[str] = None,
    output_dir: Optional[Path] = None,
) -> Dict[str, Path]:
    """Pobiera dokumentację API drona i mapę terenu, zapisuje je w folderze files."""
    key = api_key or require_api_key()
    out_dir = output_dir or FILES_DIR
    out_dir.mkdir(parents=True, exist_ok=True)

    artifacts = {
        "drone.html": (DRONE_DOC_URL, out_dir / "drone.html"),
        "drone.png": (drone_map_url(key), out_dir / "drone.png"),
    }

    saved: Dict[str, Path] = {}
    for filename, (url, path) in artifacts.items():
        saved[filename] = download_file(url, path)

    return saved
