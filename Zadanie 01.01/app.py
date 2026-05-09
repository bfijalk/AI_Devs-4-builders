import os
import io
import json

import requests
import pandas as pd
from openai import OpenAI
from dotenv import load_dotenv

load_dotenv()

API_KEY = os.getenv("API_KEY")
DATA_URL = f"https://hub.ag3nts.org/data/{API_KEY}/people.csv"

PROMPT_FILE = os.path.join(os.path.dirname(__file__), "prompt.txt")

llm_client = OpenAI(
    api_key=os.getenv("OPEN_ROUTER_API_KEY"),
    base_url=os.getenv("OPEN_ROUTER_BASE_URL"),
)


def fetch_people() -> pd.DataFrame:
    response = requests.get(DATA_URL)
    response.raise_for_status()
    df = pd.read_csv(io.StringIO(response.text))
    return df


def filter_people(df: pd.DataFrame) -> pd.DataFrame:
    CURRENT_YEAR = 2026
    MIN_BIRTH_YEAR = CURRENT_YEAR - 40  # 1986
    MAX_BIRTH_YEAR = CURRENT_YEAR - 20  # 2006

    birth_year = pd.to_datetime(df["birthDate"]).dt.year

    filtered = df[
        (df["gender"] == "M")
        & (birth_year >= MIN_BIRTH_YEAR)
        & (birth_year <= MAX_BIRTH_YEAR)
        & (df["birthPlace"].str.lower() == "grudziądz")
    ]
    return filtered.reset_index(drop=True)


TAGS_SCHEMA = {
    "type": "json_schema",
    "json_schema": {
        "name": "job_tags",
        "strict": True,
        "schema": {
            "type": "object",
            "properties": {
                "results": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "index": {"type": "integer"},
                            "tags": {
                                "type": "array",
                                "items": {
                                    "type": "string",
                                    "enum": [
                                        "IT",
                                        "transport",
                                        "edukacja",
                                        "medycyna",
                                        "praca z ludźmi",
                                        "praca z pojazdami",
                                        "praca fizyczna",
                                    ],
                                },
                            },
                        },
                        "required": ["index", "tags"],
                        "additionalProperties": False,
                    },
                }
            },
            "required": ["results"],
            "additionalProperties": False,
        },
    },
}


def tag_jobs(df: pd.DataFrame) -> list[dict]:
    with open(PROMPT_FILE, "r") as f:
        system_prompt = f.read()

    job_list = "\n".join(
        f"{i + 1}. {row['job']}" for i, row in df.iterrows()
    )

    model = os.getenv("OPEN_ROUTER_MODEL", "gpt-4o-mini")
    print(f"\nWysyłam {len(df)} opisów do LLM ({model})...")

    response = llm_client.chat.completions.create(
        model=model,
        messages=[
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": job_list},
        ],
        response_format=TAGS_SCHEMA,
    )

    raw = response.choices[0].message.content
    parsed = json.loads(raw)
    return parsed["results"]


def select_transport_people(df: pd.DataFrame, tag_results: list[dict]) -> list[dict]:
    selected = []
    for entry in tag_results:
        if "transport" not in entry["tags"]:
            continue
        idx = entry["index"] - 1
        row = df.iloc[idx]
        birth_year = pd.to_datetime(row["birthDate"]).year
        selected.append({
            "name": row["name"],
            "surname": row["surname"],
            "gender": row["gender"],
            "born": birth_year,
            "city": row["birthPlace"],
            "tags": entry["tags"],
        })
    return selected


VERIFY_URL = "https://hub.ag3nts.org/verify"


def submit_answer(people: list[dict]) -> dict:
    payload = {
        "apikey": API_KEY,
        "task": "people",
        "answer": people,
    }
    print(f"\nWysyłam {len(people)} osób na {VERIFY_URL}...")
    print(f"Payload:\n{json.dumps(payload, indent=2, ensure_ascii=False)}\n")

    response = requests.post(VERIFY_URL, json=payload)
    response.raise_for_status()
    result = response.json()
    print(f"Odpowiedź: {json.dumps(result, indent=2, ensure_ascii=False)}")
    return result


def display_people(df: pd.DataFrame, label: str = "Dane") -> None:
    print(f"\n--- {label} ({len(df)} rekordów) ---\n")
    if df.empty:
        print("Brak rekordów.")
        return
    pd.set_option("display.max_columns", None)
    pd.set_option("display.width", None)
    pd.set_option("display.max_colwidth", 60)
    print(df.to_string(index=False))


if __name__ == "__main__":
    df = fetch_people()

    filtered = filter_people(df)
    display_people(filtered, label="Po filtracji (M, 20-40 lat, Grudziądz)")

    tag_results = tag_jobs(filtered)

    print(f"\n--- Wyniki tagowania ({len(tag_results)} rekordów) ---\n")
    multi_tag_count = 0
    for entry in tag_results:
        idx = entry["index"] - 1
        row = filtered.iloc[idx]
        tags = entry["tags"]
        marker = " ◀ MULTI" if len(tags) > 1 else ""
        if len(tags) > 1:
            multi_tag_count += 1
        print(f"  {row['name']} {row['surname']}: {tags}{marker}")

    print(f"\n--- Statystyki tagowania ---")
    print(f"  Łącznie osób: {len(tag_results)}")
    print(f"  Z wieloma tagami: {multi_tag_count}")
    print(f"  Z jednym tagiem: {len(tag_results) - multi_tag_count}")

    transport_people = select_transport_people(filtered, tag_results)

    print(f"\n--- Osoby z tagiem 'transport' ({len(transport_people)}) ---\n")
    for person in transport_people:
        print(f"  {person['name']} {person['surname']} (ur. {person['born']}, {person['city']}): {person['tags']}")

    result = submit_answer(transport_people)
