"""Parsowanie odpowiedzi LLM i walidacja współrzędnych siatki."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from typing import Dict


@dataclass(frozen=True)
class GridSector:
    column: int
    row: int

    def as_dict(self) -> Dict[str, int]:
        return {"column": self.column, "row": self.row}


def strip_markdown_json(content: str) -> str:
    content = content.strip()
    if content.startswith("```"):
        content = re.sub(r"^```(?:json)?\s*", "", content)
        content = re.sub(r"\s*```$", "", content)
    return content.strip()


def parse_sector_response(content: str) -> GridSector:
    """Parsuje odpowiedź LLM do współrzędnych sektora siatki."""
    content = strip_markdown_json(content)

    try:
        data = json.loads(content)
        return GridSector(column=int(data["column"]), row=int(data["row"]))
    except (json.JSONDecodeError, KeyError, TypeError, ValueError):
        pass

    patterns = (
        r'"?column"?\s*[:=]\s*(\d+).*?"?row"?\s*[:=]\s*(\d+)',
        r'(?:x|kolumna|column)\s*[:=]?\s*(\d+).*(?:y|wiersz|row)\s*[:=]?\s*(\d+)',
    )
    for pattern in patterns:
        match = re.search(pattern, content, re.IGNORECASE | re.DOTALL)
        if match:
            return GridSector(column=int(match.group(1)), row=int(match.group(2)))

    raise ValueError(f"Nie udało się odczytać współrzędnych z odpowiedzi LLM: {content!r}")


def validate_sector(sector: GridSector, columns: int, rows: int) -> GridSector:
    if not (1 <= sector.column <= columns):
        raise ValueError(f"Kolumna {sector.column} poza zakresem 1-{columns}")
    if not (1 <= sector.row <= rows):
        raise ValueError(f"Wiersz {sector.row} poza zakresem 1-{rows}")
    return sector
