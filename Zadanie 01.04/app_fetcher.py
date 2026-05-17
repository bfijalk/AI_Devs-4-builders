import log
import agent_fetcher as agent

INDEX_URL = "https://hub.ag3nts.org/dane/doc/index.md"


def main() -> None:
    log.info("Agent dokumentacyjny uruchomiony.")
    log.info(f"Pierwsze zadanie: pobranie dokumentacji z {INDEX_URL}")

    user_message = (
        f"Pobierz dokumentację z adresu {INDEX_URL}. "
        "Sprawdź, czy w pobranych plikach są referencje do innych plików i pobierz je również. "
        "Zapisz wszystko w folderze files/. "
        "Następnie dla każdego pobranego pliku graficznego (png, jpg, jpeg, gif, webp) "
        "wywołaj image_to_docs, aby wygenerować jego dokumentację w formacie Markdown."
    )

    try:
        reply = agent.run(user_message)
        print("\n" + "═" * 56)
        print(reply)
        print("═" * 56)
    except Exception as exc:
        log.error("main", exc)
        raise


if __name__ == "__main__":
    main()
