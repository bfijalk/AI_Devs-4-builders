# Podsumowanie wykonanych prac - Zadanie 01.01

## Zrealizowane etapy

### Etap 1: Konfiguracja projektu
- `app.py`, `.env`, `requirements.txt`, virtualenv (Python 3.13)

### Etap 2: Pobranie danych
- `fetch_people()` - pobiera CSV (24417 rekordow)
- `display_people(df, label)` - wyswietla tabele

### Etap 3: Filtrowanie danych
- `filter_people(df)` - M, 20-40 lat, Grudziadz (31 rekordow)

### Etap 4: Tagowanie przez LLM
- `tag_jobs(df)` - batch do OpenRouter, Structured Output
- Model: gpt-4o, prompt w `prompt.txt`

### Etap 5: Wybor osob z tagiem "transport"
- `select_transport_people(df, tag_results)`

### Etap 6: Wyslanie odpowiedzi
- `submit_answer(people)` - POST na /verify
