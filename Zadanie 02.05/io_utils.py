"""Operacje wejścia/wyjścia: pliki, HTML, obrazy."""

from __future__ import annotations

import base64
import json
from pathlib import Path
from typing import Any, Dict, Union

import requests

from config import HTTP_TIMEOUT


def html_to_text(html: str) -> str:
    import re

    text = re.sub(r"<script[^>]*>.*?</script>", " ", html, flags=re.DOTALL | re.IGNORECASE)
    text = re.sub(r"<style[^>]*>.*?</style>", " ", text, flags=re.DOTALL | re.IGNORECASE)
    text = re.sub(r"<[^>]+>", " ", text)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def resolve_image_url(image: Union[str, Path]) -> str:
    """Zwraca URL HTTP albo data-URL dla lokalnego pliku."""
    value = str(image)
    if value.startswith(("http://", "https://", "data:")):
        return value

    path = Path(value)
    if not path.exists():
        raise FileNotFoundError(f"Nie znaleziono pliku obrazu: {path}")

    mime = "image/png" if path.suffix.lower() == ".png" else "image/jpeg"
    encoded = base64.b64encode(path.read_bytes()).decode("ascii")
    return f"data:{mime};base64,{encoded}"


def save_json(path: Path, payload: Dict[str, Any]) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def load_json(path: Path) -> Dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def download_file(url: str, destination: Path) -> Path:
    print(f"[GET] {url}")
    response = requests.get(url, timeout=HTTP_TIMEOUT)
    response.raise_for_status()

    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.suffix.lower() == ".png":
        destination.write_bytes(response.content)
    else:
        destination.write_text(response.text, encoding="utf-8")

    print(f"[OK]  Saved to {destination}")
    return destination
