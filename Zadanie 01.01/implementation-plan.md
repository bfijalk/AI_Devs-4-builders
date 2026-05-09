# Plan implementacji - Zadanie 01.01 (people)

## Cel

Aplikacja w Pythonie, która pobiera dane osób z pliku CSV, filtruje je według kryteriów, taguje zawody za pomocą LLM i wysyła odpowiedź do API.

---

## Etap 1: Konfiguracja projektu

- Utworzyć plik `app.py` jako główny skrypt
- Utworzyć plik `.env` z kluczem API (`API_KEY`)
- Zainstalować zależności: `requests`, `pandas`, `openai`, `python-dotenv`
- Utworzyć `requirements.txt`

## Etap 2: Pobranie danych

- Pobrać plik CSV z huba (wstawiając klucz API)
- Wczytać dane do DataFrame (pandas)
- Rozpoznać strukturę kolumn (name, surname, gender, birthDate, birthPlace, birthCountry, job)

## Etap 3: Filtrowanie danych

1. **Płeć** — tylko mężczyźni (`gender == "M"`)
2. **Wiek** — urodzeni między 1986 a 2006 (w 2026 roku mają 20-40 lat)
3. **Miasto urodzenia** — `birthPlace == "Grudziądz"`

## Etap 4: Tagowanie zawodów przez LLM

- Prompt z listą 7 tagów i ich opisami
- Batch wysyłki opisów stanowisk do OpenAI API
- Structured Output z JSON Schema

## Etap 5: Wybór osób z tagiem "transport"

- Filtr w Pythonie po tagu "transport"

## Etap 6: Wysłanie odpowiedzi

- POST na `https://hub.ag3nts.org/verify`
- Odbiór flagi `{FLG:...}`
