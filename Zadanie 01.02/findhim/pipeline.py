import io
import json
import os

import pandas as pd
import requests
from openai import OpenAI

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


class PeoplePipeline:
    def __init__(self, api_key: str, llm_client: OpenAI, prompt_file: str, results_dir: str, model: str):
        self.api_key = api_key
        self.llm_client = llm_client
        self.prompt_file = prompt_file
        self.results_dir = results_dir
        self.model = model
        self.data_url = f"https://hub.ag3nts.org/data/{api_key}/people.csv"

    def fetch_people(self) -> pd.DataFrame:
        response = requests.get(self.data_url)
        response.raise_for_status()
        return pd.read_csv(io.StringIO(response.text))

    def filter_people(self, df: pd.DataFrame) -> pd.DataFrame:
        current_year = 2026
        birth_year = pd.to_datetime(df["birthDate"]).dt.year
        filtered = df[
            (df["gender"] == "M")
            & (birth_year >= current_year - 40)
            & (birth_year <= current_year - 20)
            & (df["birthPlace"].str.lower() == "grudziądz")
        ]
        return filtered.reset_index(drop=True)

    def tag_jobs(self, df: pd.DataFrame) -> list[dict]:
        with open(self.prompt_file, "r") as f:
            system_prompt = f.read()

        job_list = "\n".join(f"{i + 1}. {row['job']}" for i, row in df.iterrows())
        print(f"\nWysyłam {len(df)} opisów do LLM ({self.model})...")

        response = self.llm_client.chat.completions.create(
            model=self.model,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": job_list},
            ],
            response_format=TAGS_SCHEMA,
        )
        parsed = json.loads(response.choices[0].message.content)
        return parsed["results"]

    def select_by_tag(self, df: pd.DataFrame, tag_results: list[dict], tag: str) -> list[dict]:
        selected = []
        for entry in tag_results:
            if tag not in entry["tags"]:
                continue
            row = df.iloc[entry["index"] - 1]
            selected.append({
                "name": row["name"],
                "surname": row["surname"],
                "gender": row["gender"],
                "born": pd.to_datetime(row["birthDate"]).year,
                "city": row["birthPlace"],
                "tags": entry["tags"],
            })
        return selected

    def save_result(self, people: list[dict], filename: str = "transport_people.json") -> str:
        os.makedirs(self.results_dir, exist_ok=True)
        output_path = os.path.join(self.results_dir, filename)
        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(people, f, indent=2, ensure_ascii=False)
        print(f"\nZapisano {len(people)} osób do: {output_path}")
        return output_path

    def display_people(self, df: pd.DataFrame, label: str = "Dane") -> None:
        print(f"\n--- {label} ({len(df)} rekordów) ---\n")
        if df.empty:
            print("Brak rekordów.")
            return
        pd.set_option("display.max_columns", None)
        pd.set_option("display.width", None)
        pd.set_option("display.max_colwidth", 60)
        print(df.to_string(index=False))

    def print_tag_stats(self, df: pd.DataFrame, tag_results: list[dict]) -> None:
        print(f"\n--- Wyniki tagowania ({len(tag_results)} rekordów) ---\n")
        multi_tag_count = 0
        for entry in tag_results:
            row = df.iloc[entry["index"] - 1]
            tags = entry["tags"]
            marker = " ◀ MULTI" if len(tags) > 1 else ""
            if len(tags) > 1:
                multi_tag_count += 1
            print(f"  {row['name']} {row['surname']}: {tags}{marker}")

        print(f"\n--- Statystyki tagowania ---")
        print(f"  Łącznie osób: {len(tag_results)}")
        print(f"  Z wieloma tagami: {multi_tag_count}")
        print(f"  Z jednym tagiem: {len(tag_results) - multi_tag_count}")
