"""Konfiguracja zadania evaluation — ścieżki i stałe."""

from __future__ import annotations

import os
from pathlib import Path

from dotenv import load_dotenv

load_dotenv()

BASE_DIR = Path(__file__).resolve().parent
FILES_DIR = BASE_DIR / "files"
INVALID_FILES_PATH = BASE_DIR / "niepoprawne.txt"
INVALID_CONTENT_PATH = BASE_DIR / "niepoprawne_content.json"
OPERATOR_NOTES_PATH = BASE_DIR / "operator_notes.json"
INVALID_LLM_PATH = BASE_DIR / "niepoprawne_llm"
RECHECK_PATH = BASE_DIR / "recheck.txt"

SENSOR_RANGES = {
    "temperature": ("temperature_K", 553, 873),
    "pressure": ("pressure_bar", 60, 160),
    "water": ("water_level_meters", 5.0, 15.0),
    "voltage": ("voltage_supply_v", 229.0, 231.0),
    "humidity": ("humidity_percent", 40.0, 80.0),
}

HUB_BASE_URL = "https://hub.ag3nts.org"
SENSORS_ZIP_URL = f"{HUB_BASE_URL}/dane/sensors.zip"
VERIFY_URL = f"{HUB_BASE_URL}/verify"

TASK_NAME = "evaluation"
API_KEY = os.getenv("API_KEY")
OPEN_ROUTER_API_KEY = os.getenv("OPEN_ROUTER_API_KEY")
OPEN_ROUTER_BASE_URL = os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1")
OPEN_ROUTER_MODEL = os.getenv("OPEN_ROUTER_MODEL", "openai/gpt-4o-mini")

HTTP_TIMEOUT = 120
LLM_BATCH_SIZE = 150


def require_api_key() -> str:
    if not API_KEY:
        raise ValueError("Brak API_KEY — ustaw zmienną w pliku .env")
    return API_KEY


def require_openrouter_key() -> str:
    if not OPEN_ROUTER_API_KEY:
        raise ValueError("Brak OPEN_ROUTER_API_KEY — ustaw zmienną w pliku .env")
    return OPEN_ROUTER_API_KEY
