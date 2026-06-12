"""Punkt 3: planowanie misji drona i wysyłka instrukcji do huba."""

import json

from drone_tools import get_dam_sector, read_drone_documentation, submit_instructions
from mission import submit_drone_mission

__all__ = [
    "get_dam_sector",
    "read_drone_documentation",
    "submit_drone_mission",
    "submit_instructions",
]


if __name__ == "__main__":
    print("Uruchamiam misję drona (punkt 3)...")
    result = submit_drone_mission()

    if result and result.get("success"):
        print("\nMisja zakończona sukcesem!")
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print("Misja nie zakończyła się sukcesem.")
