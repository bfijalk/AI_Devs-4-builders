import json
import os

from findhim.config import API_KEY, MODEL, PROMPT_FILE, RESULTS_DIR, get_llm_client
from findhim.pipeline import PeoplePipeline

TRANSPORT_PEOPLE_FILE = os.path.join(os.path.dirname(__file__), "..", "..", "Results", "transport_people.json")


def get_transport_people() -> list[dict]:
    with open(TRANSPORT_PEOPLE_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


def refresh_people() -> list[dict]:
    pipeline = PeoplePipeline(
        api_key=API_KEY,
        llm_client=get_llm_client(),
        prompt_file=PROMPT_FILE,
        results_dir=RESULTS_DIR,
        model=MODEL,
    )
    df = pipeline.fetch_people()
    filtered = pipeline.filter_people(df)
    tag_results = pipeline.tag_jobs(filtered)
    transport_people = pipeline.select_by_tag(filtered, tag_results, tag="transport")
    pipeline.save_result(transport_people)
    return transport_people


GET_TOOL_DEFINITION = {
    "type": "function",
    "function": {
        "name": "get_transport_people",
        "description": (
            "Zwraca listę mężczyzn urodzonych w Grudziądzu (wiek 20-40 lat w 2026 r.), "
            "którzy pracują w branży transportowej. "
            "Każda osoba zawiera pola: name, surname, gender, born, city, tags."
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

REFRESH_TOOL_DEFINITION = {
    "type": "function",
    "function": {
        "name": "refresh_people",
        "description": (
            "Pobiera świeże dane z API, filtruje je i nadpisuje lokalny plik transport_people.json. "
            "Używaj gdy dane mogą być nieaktualne lub gdy chcesz odświeżyć listę osób."
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

TOOL_DEFINITIONS = [GET_TOOL_DEFINITION, REFRESH_TOOL_DEFINITION]
