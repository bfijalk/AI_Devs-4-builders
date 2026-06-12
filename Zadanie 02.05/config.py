"""Konfiguracja zadania drone — stałe, ścieżki i zmienne środowiskowe."""

from __future__ import annotations

import os
from pathlib import Path
from typing import Optional

from dotenv import load_dotenv

load_dotenv()

BASE_DIR = Path(__file__).resolve().parent
FILES_DIR = BASE_DIR / "files"

# Hub
HUB_BASE_URL = "https://hub.ag3nts.org"
VERIFY_URL = f"{HUB_BASE_URL}/verify"
DRONE_DOC_URL = f"{HUB_BASE_URL}/dane/drone.html"

# Zadanie
TASK_NAME = "drone"
PLANT_ID = "PWR6132PL"
GRID_COLUMNS = 3
GRID_ROWS = 4

# Pliki artefaktów
DRONE_HTML_PATH = FILES_DIR / "drone.html"
DRONE_MAP_PATH = FILES_DIR / "drone.png"
DAM_SECTOR_PATH = FILES_DIR / "dam_sector.json"
MISSION_RESULT_PATH = FILES_DIR / "drone_mission_result.json"

# Klucze API
API_KEY = os.getenv("API_KEY")
OPEN_ROUTER_API_KEY = os.getenv("OPEN_ROUTER_API_KEY")
OPEN_ROUTER_BASE_URL = os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1")
OPEN_ROUTER_MODEL = os.getenv("OPEN_ROUTER_MODEL", "openai/gpt-4o-mini")

VISION_MODELS = [
    model.strip()
    for model in os.getenv("VISION_MODELS", "openai/gpt-5.4,openai/gpt-4o").split(",")
    if model.strip()
]

AGENT_MAX_STEPS = 30
HTTP_TIMEOUT = 120


def require_api_key() -> str:
    if not API_KEY:
        raise ValueError("Brak API_KEY — ustaw zmienną w pliku .env")
    return API_KEY


def require_openrouter_key() -> str:
    if not OPEN_ROUTER_API_KEY:
        raise ValueError("Brak OPEN_ROUTER_API_KEY — ustaw zmienną w pliku .env")
    return OPEN_ROUTER_API_KEY


def drone_map_url(api_key: Optional[str] = None) -> str:
    key = api_key or require_api_key()
    return f"{HUB_BASE_URL}/data/{key}/drone.png"
