"""Narzędzia agenta misji drona."""

from __future__ import annotations

from typing import Any, Callable, Dict, List

from config import DAM_SECTOR_PATH, DRONE_HTML_PATH
from hub_client import submit_drone_instructions
from io_utils import html_to_text, load_json

ToolHandler = Callable[..., Dict[str, Any]]


def read_drone_documentation() -> Dict[str, str]:
    """Czyta dokumentację API drona z pliku files/drone.html."""
    if not DRONE_HTML_PATH.exists():
        raise FileNotFoundError(
            f"Brak dokumentacji: {DRONE_HTML_PATH}. Uruchom najpierw download_drone_artifacts()."
        )
    html = DRONE_HTML_PATH.read_text(encoding="utf-8")
    return {
        "source": str(DRONE_HTML_PATH),
        "documentation": html_to_text(html),
    }


def get_dam_sector() -> Dict[str, int]:
    """Zwraca współrzędne sektora z tamą z pliku files/dam_sector.json."""
    if not DAM_SECTOR_PATH.exists():
        raise FileNotFoundError(
            f"Brak wyniku analizy mapy: {DAM_SECTOR_PATH}. Uruchom najpierw locate_dam_sector()."
        )
    data = load_json(DAM_SECTOR_PATH)
    return {"column": int(data["column"]), "row": int(data["row"])}


def submit_instructions(instructions: List[str]) -> Dict[str, Any]:
    """Wysyła sekwencję instrukcji drona do endpointu /verify."""
    return submit_drone_instructions(instructions)


TOOL_DEFINITIONS = [
    {
        "type": "function",
        "function": {
            "name": "read_drone_documentation",
            "description": "Czyta dokumentację API drona DRN-BMB7 z pliku files/drone.html.",
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_dam_sector",
            "description": (
                "Zwraca współrzędne sektora siatki z tamą (column=x, row=y), "
                "wynik wcześniejszej analizy mapy."
            ),
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "submit_instructions",
            "description": (
                "Wysyła sekwencję instrukcji drona do huba (/verify). "
                "Wywołuj dopóki nie otrzymasz success=true z flagą."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "instructions": {
                        "type": "array",
                        "items": {"type": "string"},
                        "description": "Tablica instrukcji w formacie z dokumentacji API",
                    }
                },
                "required": ["instructions"],
            },
        },
    },
]

TOOL_MAP: Dict[str, ToolHandler] = {
    "read_drone_documentation": read_drone_documentation,
    "get_dam_sector": get_dam_sector,
    "submit_instructions": submit_instructions,
}
