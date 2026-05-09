import os
from typing import Optional

import requests

from findhim.config import API_KEY

LOCATIONS_URL = "https://hub.ag3nts.org/data/{api_key}/findhim_locations.json"
PERSON_LOCATION_URL = "https://hub.ag3nts.org/api/location"
ACCESS_LEVEL_URL = "https://hub.ag3nts.org/api/accesslevel"


def fetch_power_plants(api_key: Optional[str] = None) -> list[dict]:
    key = api_key or API_KEY
    response = requests.get(LOCATIONS_URL.format(api_key=key))
    response.raise_for_status()
    raw = response.json()
    return [
        {
            "city": city,
            "code": details["code"],
            "power": details["power"],
            "is_active": details["is_active"],
        }
        for city, details in raw["power_plants"].items()
    ]


def get_person_location(name: str, surname: str, api_key: Optional[str] = None) -> Optional[dict]:
    key = api_key or API_KEY
    response = requests.post(
        PERSON_LOCATION_URL,
        json={"apikey": key, "name": name, "surname": surname},
    )
    response.raise_for_status()
    locations = response.json()
    if not locations:
        return None
    return locations[-1]


def get_person_access_level(name: str, surname: str, birth_year: int, api_key: Optional[str] = None) -> dict:
    key = api_key or API_KEY
    response = requests.post(
        ACCESS_LEVEL_URL,
        json={"apikey": key, "name": name, "surname": surname, "birthYear": birth_year},
    )
    response.raise_for_status()
    return response.json()


POWER_PLANTS_TOOL_DEFINITION = {
    "type": "function",
    "function": {
        "name": "fetch_power_plants",
        "description": (
            "Pobiera listę elektrowni wraz z ich kodami identyfikacyjnymi, mocą i statusem aktywności. "
            "Zwraca listę obiektów z polami: city, code, power, is_active."
        ),
        "parameters": {
            "type": "object",
            "properties": {},
            "required": [],
            "additionalProperties": False,
        },
        "strict": True,
    },
}

PERSON_LOCATION_TOOL_DEFINITION = {
    "type": "function",
    "function": {
        "name": "get_person_location",
        "description": (
            "Zwraca ostatnią zarejestrowaną lokalizację (koordynaty) danej osoby. "
            "Przyjmuje imię i nazwisko, zwraca obiekt z polami latitude i longitude "
            "lub null jeśli brak danych."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Imię osoby"},
                "surname": {"type": "string", "description": "Nazwisko osoby"},
            },
            "required": ["name", "surname"],
            "additionalProperties": False,
        },
        "strict": True,
    },
}

ACCESS_LEVEL_TOOL_DEFINITION = {
    "type": "function",
    "function": {
        "name": "get_person_access_level",
        "description": (
            "Zwraca poziom dostępu danej osoby. "
            "Przyjmuje imię, nazwisko i rok urodzenia. "
            "Zwraca obiekt z polami: name, surname, accessLevel."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Imię osoby"},
                "surname": {"type": "string", "description": "Nazwisko osoby"},
                "birth_year": {"type": "integer", "description": "Rok urodzenia osoby"},
            },
            "required": ["name", "surname", "birth_year"],
            "additionalProperties": False,
        },
        "strict": True,
    },
}

TOOL_DEFINITIONS = [POWER_PLANTS_TOOL_DEFINITION, PERSON_LOCATION_TOOL_DEFINITION, ACCESS_LEVEL_TOOL_DEFINITION]
