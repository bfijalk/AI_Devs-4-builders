"""
Pełne wykonanie zadania end-to-end:

  FAZA 1 — Agent dokumentacyjny (app.py)
    Pobiera index.md ze zdalnego serwera wraz ze wszystkimi
    powiązanymi plikami. Dla plików graficznych generuje .md.
    Wynik trafia do folderu files/.

  FAZA 2 — Agent deklaracji SPK (app2.py)
    Na podstawie pobranych plików:
      - ustala kod trasy (dynamicznie z index.md)
      - dobiera kategorię przesyłki (0 PP)
      - oblicza opłatę i WDP
      - wypełnia deklarację wg wzoru z zalacznik-E.md
      - wysyła na https://hub.ag3nts.org/verify
"""

import os
import sys
from pathlib import Path
from dotenv import load_dotenv

import log
import agent_fetcher as agent
import agent_declaration as agent2
import tools_declaration as tools2

load_dotenv()

INDEX_URL = "https://hub.ag3nts.org/dane/doc/index.md"

# Dane przesyłki (z task.md)
SHIPMENT = {
    "origin": "Gdańsk",
    "destination": "Żarnowiec",
    "sender_id": "450202122",
    "weight_kg": 2800,
    "description": "kasety z paliwem do reaktora",
    "notes": "brak",
}

_SEP = "═" * 56
_SEP_THIN = "─" * 56


def _header(title: str) -> None:
    print(f"\n{_SEP}")
    print(f"  {title}")
    print(_SEP)


def _result(label: str, text: str) -> None:
    print(f"\n{_SEP_THIN}")
    print(f"  {label}")
    print(_SEP_THIN)
    print(text)
    print(_SEP_THIN)


def phase1_fetch_docs() -> None:
    """Faza 1: pobierz dokumentację i zapisz do files/."""
    _header("FAZA 1 — Agent dokumentacyjny")
    log.info(f"URL startowy: {INDEX_URL}")

    user_message = (
        f"Pobierz dokumentację z adresu {INDEX_URL}. "
        "Sprawdź, czy w pobranych plikach są referencje do innych plików i pobierz je również. "
        "Zapisz wszystko w folderze files/. "
        "Następnie dla każdego pobranego pliku graficznego (png, jpg, jpeg, gif, webp) "
        "wywołaj image_to_docs, aby wygenerować jego dokumentację w formacie Markdown."
    )

    reply = agent.run(user_message)
    _result("Odpowiedź agenta dokumentacyjnego", reply)

    files = sorted(Path(__file__).parent.glob("files/*"))
    log.info(f"Folder files/ zawiera {len(files)} plików po fazie 1.")


def phase2_declaration() -> None:
    """Faza 2: wypełnij deklarację i wyślij na /verify."""
    _header("FAZA 2 — Agent deklaracji SPK")
    log.info(
        f"Przesyłka: {SHIPMENT['origin']} → {SHIPMENT['destination']}, "
        f"{SHIPMENT['weight_kg']} kg, nadawca: {SHIPMENT['sender_id']}"
    )

    # Wyczyść cache grafu — upewnij się, że agent używa świeżo pobranych plików
    tools2._graph_cache = None
    tools2._regions_cache = None

    reply = agent2.run(SHIPMENT)
    _result("Odpowiedź agenta deklaracji", reply)


def main() -> None:
    print(f"\n{'█' * 56}")
    print("  TASK EXECUTION — Zadanie 01.04 (e2e)")
    print(f"{'█' * 56}")

    try:
        phase1_fetch_docs()
    except Exception as exc:
        log.error("Faza 1 zakończona błędem", exc)
        sys.exit(1)

    try:
        phase2_declaration()
    except Exception as exc:
        log.error("Faza 2 zakończona błędem", exc)
        sys.exit(1)

    print(f"\n{'█' * 56}")
    print("  ZAKOŃCZONO POMYŚLNIE")
    print(f"{'█' * 56}\n")


if __name__ == "__main__":
    main()
