# Plan implementacji — Zadanie 01.04, punkt 2

## Cel

Drugi agent Python, który:
1. Analizuje dokumentację z folderu `files/` i ustala prawidłowy kod trasy
2. Dobiera kategorię przesyłki tak, aby koszt wyniósł **0 PP** (finansowane przez System)
3. Oblicza opłatę zgodnie z regulaminem SPK
4. Wypełnia wzór deklaracji z `zalacznik-E.md`, zachowując oryginalny format
5. Wysyła gotową deklarację na `https://hub.ag3nts.org/verify`

---

## Dane przesyłki (ustalone na podstawie zadania)

Zadanie nie podaje konkretnych danych przesyłki — agent musi je wywnioskować lub przyjąć sensowne
wartości domyślne. Na potrzeby implementacji przyjmujemy następujące dane startowe (parametryzowane):

| Pole | Wartość domyślna |
|---|---|
| Punkt nadawczy | Warszawa |
| Punkt docelowy | Kraków |
| Nadawca | ID-2041-WW |
| Masa (kg) | 2 |
| Opis zawartości | Dokumenty urzędowe |
| WDP | 0 |
| Uwagi specjalne | Brak |

Parametry możliwe do podania przez użytkownika przy uruchomieniu `app2.py`.

---

## Kluczowe reguły z dokumentacji

### Kategoria z zerowym kosztem (budżet = 0 PP)
- **Kategoria A** — Strategiczna: OB = 0 PP (pokrywana przez System)
- **Kategoria B** — Medyczna: OB = 0 PP (pokrywana przez System)
- Dodatkowo: kat. A i B **zwolnione z opłat** w całości (§ 9.4)
- → Agent wybierze **kategorię A** jako pierwszą dostępną z zerowym kosztem całkowitym

### Wyznaczanie trasy
- Trasy magistralne (M), regionalne (R), lokalne (L) — lista w `index.md` sekcja 3
- Graf połączeń: city → [(trasa, city, km), ...]
- Algorytm: BFS/Dijkstra po grafie tras, szukamy najkrótszej ścieżki
- Kod trasy: sekwencja kodów tras (np. `M-02` dla Warszawa–Kraków bezpośrednio)
- Trasy wyłączone: X-01..X-08 — omijane

### Obliczanie opłaty
```
OB (opłata bazowa):
  A → 0 PP, B → 0 PP, C → 2 PP, D → 5 PP, E → 10 PP

OW (opłata wagowa):
  0.1–5 kg   → 0.5 PP/kg
  5.1–25 kg  → 1 PP/kg
  25.1–100 kg → 2 PP/kg
  100.1–500 kg → 3 PP/kg
  500.1–1000 kg → 5 PP/kg
  1000+ kg   → 7 PP/kg + opłata za wagony dodatkowe

OT (opłata trasowa, za 100 km zaokrąglone w górę):
  1 region   → 1 PP/100 km
  2 regiony  → 2 PP/100 km
  3+ regiony → 3 PP/100 km

Razem = OB + OW + OT
Kat. A i B → Razem = 0 PP (zwolnienie z opłat)
```

### Regiony
- Północny: Gdańsk, Szczecin, Bydgoszcz, Toruń, Elbląg, Olsztyn
- Centralny: Warszawa, Łódź, Poznań, Białystok
- Południowy: Kraków, Katowice, Wrocław, Częstochowa, Kielce, Rzeszów, Lublin
- (uproszczony podział — agent ustali na podstawie mapy z `zalacznik-F.md` i `index.md`)

---

## Architektura

### Nowe pliki

```
Zadanie 01.04/
├── agent2.py        — pętla agentyczna dla agenta deklaracji
├── tools2.py        — narzędzia: read_file, find_route, calculate_fee, fill_declaration, send_to_verify
└── app2.py          — punkt wejścia
```

Współdzielone: `log.py` (bez zmian).

### Narzędzia agenta (`tools2.py`)

#### `read_file(filename)`
- Wczytuje plik z folderu `files/` i zwraca jego treść jako tekst
- Umożliwia agentowi przeglądanie dokumentacji

#### `find_route(origin, destination)`
- Parsuje listę tras z `files/index.md`
- Buduje graf połączeń i szuka najkrótszej ścieżki (BFS po liczbie przesiadek, tie-break po km)
- Zwraca: kod trasy, łączną długość (km), liczbę przekraczanych granic regionów

#### `calculate_fee(category, weight_kg, distance_km, region_crossings)`
- Liczy opłatę wg regulaminu SPK
- Zwraca: OB, OW, OT, razem

#### `fill_declaration(data)`
- Przyjmuje słownik z polami deklaracji
- Formatuje tekst ściśle wg wzoru z `zalacznik-E.md` (zachowanie ramek `===` i `---`)
- Zwraca wypełniony tekst deklaracji (string)
- Zapisuje też do `files/deklaracja.md`

#### `send_to_verify(declaration_text)`
- POSTuje na `https://hub.ag3nts.org/verify`
- Payload: `{"apikey": API_KEY, "task": "sendit", "answer": {"declaration": declaration_text}}`
- Zwraca odpowiedź serwera

---

## Sekwencja kroków agenta

```
1. read_file("index.md")          ← pobierz trasy i zasady
2. read_file("zalacznik-E.md")    ← pobierz wzór deklaracji
3. find_route(origin, dest)       ← ustal kod trasy i odległość
4. calculate_fee("A", mass, km, crossings)  ← koszt = 0 PP
5. fill_declaration({...})        ← wypełnij deklarację
6. send_to_verify(declaration)    ← wyślij na /verify
```

---

## Format deklaracji (docelowy output)

```
SYSTEM PRZESYŁEK KONDUKTORSKICH - DEKLARACJA ZAWARTOŚCI
======================================================
DATA: 2026-05-17
PUNKT NADAWCZY: Warszawa
------------------------------------------------------
NADAWCA: ID-2041-WW
PUNKT DOCELOWY: Kraków
TRASA: M-02
------------------------------------------------------
KATEGORIA PRZESYŁKI: A
------------------------------------------------------
OPIS ZAWARTOŚCI (max 200 znaków): Dokumenty urzędowe
------------------------------------------------------
DEKLAROWANA MASA (kg): 2
------------------------------------------------------
WDP: 0
------------------------------------------------------
UWAGI SPECJALNE: Brak
------------------------------------------------------
KWOTA DO ZAPŁATY: 0 PP
------------------------------------------------------
OŚWIADCZAM, ŻE PODANE INFORMACJE SĄ PRAWDZIWE.
BIORĘ NA SIEBIE KONSEKWENCJĘ ZA FAŁSZYWE OŚWIADCZENIE.
======================================================
```

---

## Kolejność implementacji

1. `tools2.py` — `read_file` + `find_route` + `calculate_fee` + `fill_declaration` + `send_to_verify`
2. `agent2.py` — pętla agentyczna z system promptem i definicjami narzędzi
3. `app2.py` — punkt wejścia z parametrami przesyłki
