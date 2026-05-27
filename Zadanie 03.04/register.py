"""Script to register tools with the centrala and verify results."""

import os
import sys
import time

import requests
from dotenv import load_dotenv

load_dotenv()

API_KEY = os.getenv("API_KEY")
CENTRALA_URL = "https://hub.ag3nts.org/verify"


def register_tools(ngrok_url: str):
    """Register tool URLs with the centrala."""
    payload = {
        "apikey": API_KEY,
        "task": "negotiations",
        "answer": {
            "tools": [
                {
                    "URL": f"{ngrok_url}/api/search_items",
                    "description": (
                        "Search for electronic components by natural language description. "
                        "Send a POST request with params containing a description of what "
                        "you're looking for (e.g. 'resistor 10 ohm', '10 meter cable', "
                        "'red LED'). Returns matching item names and their codes."
                    ),
                },
                {
                    "URL": f"{ngrok_url}/api/find_cities",
                    "description": (
                        "Find cities that have ALL specified items in stock. "
                        "Send a POST request with params containing comma-separated item "
                        "codes (e.g. 'BWST28, 2GF4VO, QQAPOK'). Returns city names that "
                        "have all the specified items simultaneously. Use item codes "
                        "obtained from the search_items tool."
                    ),
                },
            ]
        },
    }

    print("Registering tools with centrala...")
    print(f"  search_items: {ngrok_url}/api/search_items")
    print(f"  find_cities:  {ngrok_url}/api/find_cities")

    response = requests.post(CENTRALA_URL, json=payload, timeout=30)
    print(f"Response [{response.status_code}]: {response.text}")


def check_result():
    """Check async verification result."""
    payload = {
        "apikey": API_KEY,
        "task": "negotiations",
        "answer": {
            "action": "check",
        },
    }

    print("Checking result...")
    response = requests.post(CENTRALA_URL, json=payload, timeout=30)
    print(f"Response [{response.status_code}]: {response.text}")


def main():
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python register.py <ngrok_url>   - Register tools")
        print("  python register.py check         - Check verification result")
        sys.exit(1)

    command = sys.argv[1]

    if command == "check":
        check_result()
    else:
        ngrok_url = command.rstrip("/")
        register_tools(ngrok_url)
        print("\nWaiting 60 seconds for agent to process...")
        time.sleep(60)
        print("\nChecking result:")
        check_result()


if __name__ == "__main__":
    main()
