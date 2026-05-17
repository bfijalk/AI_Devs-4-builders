import os
from dotenv import load_dotenv

import log
import agent_declaration as agent2

load_dotenv()

# Dane przesyłki (z task.md)
SHIPMENT = {
    "origin": "Gdańsk",
    "destination": "Żarnowiec",
    "sender_id": "450202122",
    "weight_kg": 2800,
    "description": "kasety z paliwem do reaktora",
    "notes": "brak",
}


def main() -> None:
    log.info("Agent deklaracji SPK uruchomiony.")
    log.info(
        f"Przesyłka: {SHIPMENT['origin']} → {SHIPMENT['destination']}, "
        f"{SHIPMENT['weight_kg']} kg, nadawca: {SHIPMENT['sender_id']}"
    )

    try:
        reply = agent2.run(SHIPMENT)
        print("\n" + "═" * 56)
        print(reply)
        print("═" * 56)
    except Exception as exc:
        log.error("main", exc)
        raise


if __name__ == "__main__":
    main()
