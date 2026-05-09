"""
Submits the proxy task to hub.ag3nts.org/verify.

Usage:
    venv/bin/python ag3nts-test.py
    venv/bin/python ag3nts-test.py --url https://your-ngrok-url.ngrok-free.app --session my-session-id
"""

import json
import argparse
import urllib.request
import urllib.error
import os
from dotenv import load_dotenv

load_dotenv()

VERIFY_URL = "https://hub.ag3nts.org/verify"
DEFAULT_NGROK_URL = "https://gangrene-egotistic-molecular.ngrok-free.dev"
DEFAULT_SESSION_ID = "session01"


def submit(ngrok_url: str, session_id: str, api_key: str) -> dict:
    payload = {
        "apikey": api_key,
        "task": "proxy",
        "answer": {
            "url": ngrok_url,
            "sessionID": session_id,
        },
    }

    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        VERIFY_URL,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    with urllib.request.urlopen(req, timeout=15) as resp:
        return json.loads(resp.read())


def main() -> None:
    parser = argparse.ArgumentParser(description="Submit proxy task to hub.ag3nts.org")
    parser.add_argument("--url", default=DEFAULT_NGROK_URL, help="Public ngrok URL of your server")
    parser.add_argument("--session", default=DEFAULT_SESSION_ID, help="Session ID to use")
    args = parser.parse_args()

    api_key = os.getenv("API_KEY", "")
    if not api_key:
        print("\x1b[31mBrak API_KEY w .env\x1b[0m")
        return

    print(f"Wysyłam zgłoszenie do {VERIFY_URL}")
    print(f"  url       = {args.url}")
    print(f"  sessionID = {args.session}")
    print()

    try:
        result = submit(args.url, args.session, api_key)
        print(json.dumps(result, indent=2, ensure_ascii=False))
    except urllib.error.HTTPError as e:
        print(f"\x1b[31mHTTP {e.code}: {e.read().decode()}\x1b[0m")
    except Exception as e:
        print(f"\x1b[31mBłąd: {e}\x1b[0m")


if __name__ == "__main__":
    main()
