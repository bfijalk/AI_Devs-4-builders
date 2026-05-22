import csv
import json
import os

import requests
from dotenv import load_dotenv

load_dotenv()

API_KEY = os.getenv("API_KEY")
VERIFY_URL = "https://hub.ag3nts.org/verify"

SEP = "-" * 60


def download_csv():
    url = f"https://hub.ag3nts.org/data/{API_KEY}/categorize.csv"
    os.makedirs("Files", exist_ok=True)

    print(f"[GET] {url}")
    response = requests.get(url)
    response.raise_for_status()

    filepath = os.path.join("Files", "categorize.csv")
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(response.text)

    print(f"[OK]  Saved to {filepath}\n")
    return filepath


def send_reset():
    payload = {
        "apikey": API_KEY,
        "task": "categorize",
        "answer": {"prompt": "reset"},
    }
    print(f"\n[RESET] Sending reset to {VERIFY_URL}")
    response = requests.post(VERIFY_URL, json=payload)
    try:
        data = response.json()
        print(f"  reset response: {json.dumps(data, ensure_ascii=False)}")
    except Exception:
        print(f"  reset response: {response.text}")


def categorize_items(filepath):
    with open(filepath, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        rows = list(reader)

    total = len(rows)
    results = {}

    print(f"\n{'='*60}")
    print(f"Processing {total} items")
    print(f"{'='*60}")

    for i, row in enumerate(rows, 1):
        item_id = row["code"]
        description = row["description"]

        prompt = (
            f"Is this a weapon or combat device designed to attack people? "
            f"DNG if yes, NEU if no. One word only. "
            f"Item ID: {item_id}. Description: {description}."
        )

        payload = {
            "apikey": API_KEY,
            "task": "categorize",
            "answer": {"prompt": prompt},
        }

        print(SEP)
        print(f"[{i}/{total}] POST {VERIFY_URL}")
        print(f"  item_id    : {item_id}")
        print(f"  description: {description}")
        print(f"  prompt     : {prompt}")

        response = requests.post(VERIFY_URL, json=payload)

        print(f"  status     : {response.status_code}")
        try:
            data = response.json()
            print(f"  response   : {json.dumps(data, ensure_ascii=False)}")
        except Exception:
            print(f"  response   : {response.text}")
            data = None

        if not response.ok:
            print(f"  [ERROR] Bad response. Stopping.")
            break

        results[item_id] = data

    print(SEP)
    print(f"\nDone. Processed {len(results)}/{total} items.")
    return results


if __name__ == "__main__":
    csv_path = download_csv()
    send_reset()
    categorize_items(csv_path)
