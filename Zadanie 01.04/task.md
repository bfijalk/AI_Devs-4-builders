1. Napisz funkcję w języku python, która pobierze zawartość pliku do którego wskazałem URL. Po pobraniu tego pliku powinna sprawdzić, czy w środku nie ma referencji do innych plików, jezeli tak to rowniez je pobrac. Wszystko powinno zostac zapisane w folderze files.
Dodatkowe pliki mogą być zdefiniowane jako .md ale mogą tez miec inne formaty. Beda natomiast referowaly do tego samego URL https://hub.ag3nts.org/dane/doc/.

Chcialbym abys zaczal budowac agenta w jezyku python, ktorego jednym z narzedzi bedzie wyzej opisana funkcja do pobierania plikow.
Pierwszy plik do pobrania i sprawdzenia, czy w srodku sa inne zalaczniki znajduje sie tutaj https://hub.ag3nts.org/dane/doc/index.md.

Program powinien tez logowac akcje, ktore wlasnie wykonuje, aby mozna bylo przesledzic jakie akcje zostaly po drodze podjete.

2. 

Napisz drugi agent python który na podstawie wzoru deklaracji, wypelni kazde pole zgodnie z danymi przesylki i regulaminem. Wszystkie pliki, ktorych moze potrzebowac ten agent znajduja sie w folderze files. Deklaracja to zalacznik-E. Format nie powinien zostac zmieniony.

Sekwencja dzialan:
    Na podstawie dokumentacji agent powinien ustalic prawidlowy kod trasy. (Wymaga sprawdzenia sieci polaczen i listy tras)
    Agent ustali oplate. Zgodnie z regulaminem SPK zawierajacym tabele oplat. Oplata zalezy od kategorii przesylki, jej wagi i przebiegu trasy. Budzet wynosi 0 pp - Agent powinien wybrac kategorie, ktora jest finansowana przez system.
    Ostatnim krokiem agenta jest wyslanie gotowego tekstu do /verify zgodnie z bazowym url.


TO sa dane niezbedne do wypelnienia deklaracji, agent powinien przeczytac je jako dane wejsciowe do programu.

Nadawca (identyfikator): 450202122
Punkt nadawczy: Gdańsk
Punkt docelowy: Żarnowiec
Waga: 2,8 tony (2800 kg)
Budżet: 0 PP (przesyłka ma być darmowa lub finansowana przez System)
Zawartość: kasety z paliwem do reaktora
Uwagi specjalne: brak - nie dodawaj żadnych uwag

---

## Co zostało zrobione

### Struktura projektu

```
Zadanie 01.04/
├── task-execution.py        — uruchamia oba agenty e2e (punkt wejścia całego zadania)
│
├── agent_fetcher.py         — agent dokumentacyjny (pobieranie plików)
├── tools_fetcher.py         — narzędzia: fetch_docs, image_to_docs
├── app_fetcher.py           — samodzielny punkt wejścia agenta dokumentacyjnego
│
├── agent_declaration.py     — agent deklaracji SPK (wypełnianie i wysyłka)
├── tools_declaration.py     — narzędzia: read_file, find_route, calculate_fee, fill_declaration, send_to_verify
├── app_declaration.py       — samodzielny punkt wejścia agenta deklaracji
│
├── log.py                   — kolorowy logger z etykietami [INFO], [FETCH], [SAVE], [REFS], [TOOL], [AI]
└── files/                   — folder na pobrane pliki dokumentacji i wygenerowaną deklarację
```

### Narzędzia agenta (`tools.py`)

#### `fetch_docs(url)`
- Pobiera plik spod podanego URL i zapisuje go w folderze `files/`
- Wykrywa referencje do innych plików w trzech formatach:
  - `[include file="zalacznik-A.md"]` — format specyficzny dla dokumentacji
  - Standardowe linki Markdown: `[tekst](plik.md)`
  - Gołe URL kończące się obsługiwanym rozszerzeniem
- Rekurencyjnie pobiera wszystkie znalezione pliki (BFS, bez duplikatów)
- Obsługiwane formaty: `.md`, `.txt`, `.pdf`, `.json`, `.csv`, `.html`, `.xml`, `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`, `.svg`, `.bmp`, `.tiff`

#### `image_to_docs(image_path)`
- Wczytuje plik graficzny (`.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`) z dysku
- Koduje go w base64 i wysyła do modelu wizyjnego (gpt-4o vision)
- Generuje szczegółową dokumentację techniczną w formacie Markdown
- Zapisuje wynik jako `<nazwa_pliku>.md` obok oryginału

### Agent (`agent.py`)
- Pętla agentyczna oparta na OpenAI-compatible API (OpenRouter)
- Model: `gpt-4o` (konfigurowalny przez `.env`)
- Maksymalnie 20 kroków, po czym rzuca `RuntimeError`
- Obsługuje wielokrotne wywołania narzędzi w jednej sesji

### Automatyczne działanie przy uruchomieniu (`app.py`)
Po uruchomieniu `python3 app.py` agent automatycznie:
1. Pobiera `https://hub.ag3nts.org/dane/doc/index.md`
2. Rekurencyjnie pobiera wszystkie powiązane pliki do `files/`
3. Dla każdego pobranego pliku graficznego wywołuje `image_to_docs` i generuje `.md`

### Logowanie (`log.py`)
Kolorowe logi z etykietami:
- `[INFO]` — informacje ogólne
- `[FETCH]` — żądania HTTP (start, sukces z rozmiarem, błąd)
- `[SAVE]` — zapis pliku na dysk
- `[REFS]` — wykryte referencje do innych plików
- `[TOOL]` — wywołania i wyniki narzędzi
- `[AI]` — komunikacja z modelem LLM
