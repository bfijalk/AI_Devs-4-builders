# Zadanie 01.02 — Podsumowanie implementacji

## Struktura plików

```
Zadanie 01.02/
├── app.py                      # Punkt wejścia — tylko konfiguracja i wywołania
├── people_pipeline.py          # Klasa PeoplePipeline — pipeline danych osobowych
├── get_people_data.py          # Narzędzia LLM do odczytu i odświeżania listy osób
├── get_powerplants_location.py # Narzędzia LLM: elektrownie, lokalizacja, poziom dostępu
├── prompt.txt                  # Systemowy prompt do tagowania zawodów przez LLM
├── .env                        # Klucze API (API_KEY, OPEN_ROUTER_API_KEY, model)
└── Results/
    └── transport_people.json   # Wynikowa lista 5 osób z tagiem "transport"
```

---

## Moduły i funkcje

### `people_pipeline.py` — klasa `PeoplePipeline`

Realizuje pełny pipeline filtrowania i tagowania osób.

| Metoda | Opis |
|---|---|
| `fetch_people()` | Pobiera `people.csv` z `hub.ag3nts.org` jako DataFrame |
| `filter_people(df)` | Filtruje: płeć M, wiek 20–40 lat (ur. 1986–2006), miasto Grudziądz |
| `tag_jobs(df)` | Wysyła opisy zawodów do LLM (Structured Output), zwraca tagi dla każdej osoby |
| `select_by_tag(df, tag_results, tag)` | Wybiera osoby posiadające wskazany tag |
| `save_result(people, filename)` | Zapisuje wynik do `Results/<filename>.json` |
| `display_people(df, label)` | Wyświetla DataFrame w konsoli |
| `print_tag_stats(df, tag_results)` | Wyświetla statystyki tagowania |

**Dostępne tagi LLM:** `IT`, `transport`, `edukacja`, `medycyna`, `praca z ludźmi`, `praca z pojazdami`, `praca fizyczna`

---

### `get_people_data.py` — narzędzia LLM dla listy osób

| Symbol | Opis |
|---|---|
| `get_transport_people()` | Odczytuje `Results/transport_people.json` z dysku |
| `refresh_people()` | Uruchamia pełny pipeline od nowa i nadpisuje plik wynikowy |
| `TOOL_DEFINITIONS` | Lista 2 definicji narzędzi gotowa do przekazania w `tools=[...]` |

**Wynik w `Results/transport_people.json` (5 osób):**

| Imię | Nazwisko | Rok ur. | Tagi |
|---|---|---|---|
| Cezary | Żurek | 1987 | transport |
| Jacek | Nowak | 1991 | transport, praca z ludźmi |
| Oskar | Sieradzki | 1993 | transport |
| Wojciech | Bielik | 1986 | transport |
| Wacław | Jasiński | 1986 | transport |

---

### `get_powerplants_location.py` — narzędzia LLM dla lokalizacji i dostępu

| Funkcja | Endpoint | Opis |
|---|---|---|
| `fetch_power_plants()` | GET `hub.ag3nts.org/data/{key}/findhim_locations.json` | Zwraca listę elektrowni z polami: `city`, `code`, `power`, `is_active` |
| `get_person_location(name, surname)` | POST `hub.ag3nts.org/api/location` | Zwraca **ostatnią** zarejestrowaną lokalizację osoby `{latitude, longitude}` |
| `get_person_access_level(name, surname, birth_year)` | POST `hub.ag3nts.org/api/accesslevel` | Zwraca poziom dostępu osoby `{name, surname, accessLevel}` |
| `TOOL_DEFINITIONS` | — | Lista 3 definicji narzędzi naraz |

**Znane elektrownie:**

| Miasto | Kod | Moc | Status |
|---|---|---|---|
| Zabrze | PWR3847PL | 35 MW | aktywna |
| Piotrków Trybunalski | PWR5921PL | 28 MW | aktywna |
| Grudziądz | PWR7264PL | 1138 MW | aktywna |
| Tczew | PWR1593PL | 31 MW | aktywna |
| Radom | PWR8406PL | 38 MW | aktywna |
| Chełmno | PWR2758PL | 128 MW | aktywna |
| Żarnowiec | PWR6132PL | 0 MW | nieaktywna |

---

## Co jeszcze do zrobienia

- [ ] Powiązanie osób z elektrowniami na podstawie lokalizacji (koordynaty → miasto)
- [ ] Odpytanie poziomów dostępu dla wszystkich 5 osób z transportu
- [ ] Zbudowanie agentowego loopa LLM korzystającego ze wszystkich narzędzi
- [ ] Wysłanie finalnej odpowiedzi do `hub.ag3nts.org/verify`
