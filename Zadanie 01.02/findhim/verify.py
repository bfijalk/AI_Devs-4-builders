import json

import requests

from findhim.config import API_KEY, VERIFY_URL

ANSWER_SCHEMA = {
    "type": "json_schema",
    "json_schema": {
        "name": "findhim_answer",
        "strict": True,
        "schema": {
            "type": "object",
            "properties": {
                "name":        {"type": "string"},
                "surname":     {"type": "string"},
                "accessLevel": {"type": "integer"},
                "powerPlant":  {"type": "string"},
            },
            "required": ["name", "surname", "accessLevel", "powerPlant"],
            "additionalProperties": False,
        },
    },
}


def submit_answer(answer: dict) -> dict:
    payload = {
        "apikey": API_KEY,
        "task": "findhim",
        "answer": answer,
    }
    print(f"\nWysyłam odpowiedź:\n{json.dumps(payload, indent=2, ensure_ascii=False)}")
    response = requests.post(VERIFY_URL, json=payload)
    result = response.json()
    print(f"\nOdpowiedź serwera: {json.dumps(result, indent=2, ensure_ascii=False)}")
    response.raise_for_status()
    return result
