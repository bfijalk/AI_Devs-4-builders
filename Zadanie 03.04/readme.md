# Zadanie 03.04 — Negotiations

Agent centralny szuka komponentów elektronicznych i musi ustalić, które miasta posiadają
jednocześnie wszystkie potrzebne elementy. Agent korzysta z naszych narzędzi (API endpoints),
które udostępniamy przez ngrok.

## Architektura

```
Agent (centrala)
  │
  ├── POST /api/search_items  ← "potrzebuję kabla 10m"
  │       → keyword prefilter + LLM matching
  │       → zwraca kody i nazwy pasujących elementów
  │
  └── POST /api/find_cities   ← "CODE1, CODE2, CODE3"
          → szuka miast mających WSZYSTKIE podane itemy
          → zwraca listę miast (intersection)
```

## Pliki

| Plik | Opis |
|------|------|
| `data_store.py` | Warstwa danych — ładuje CSV, buduje indeksy, realizuje zapytania |
| `llm_search.py` | Wyszukiwanie: keyword prefilter + LLM (OpenRouter/gpt-4.1) |
| `server.py` | Serwer Flask (port 5050) z endpointami dla agenta |
| `register.py` | Rejestracja narzędzi w centrali + weryfikacja wyniku |
| `files/` | Pliki CSV ze źródłami wiedzy (cities, items, connections) |

## Dane (files/)

- **cities.csv** — 51 miast polskich z 6-znakowymi kodami
- **items.csv** — 2137 komponentów elektronicznych z kodami
- **connections.csv** — 5350 relacji item↔city (tabela asocjacyjna M:N)

## Uruchomienie

### 1. Instalacja zależności

```bash
pip install -r requirements.txt
```

### 2. Konfiguracja (.env)

```
API_KEY=twoj-klucz-centrali
OPEN_ROUTER_API_KEY=sk-or-...
OPEN_ROUTER_MODEL=openai/gpt-4.1
OPEN_ROUTER_BASE_URL=https://openrouter.ai/api/v1
```

### 3. Uruchomienie serwera

```bash
python3 server.py
```

### 4. Tunel ngrok (osobny terminal)

```bash
ngrok http 5050
```

### 5. Rejestracja narzędzi

```bash
python3 register.py https://TWOJ-ADRES.ngrok-free.app
```

Skrypt zarejestruje endpointy, poczeka 60s na agenta, a potem sprawdzi wynik.

### 6. Sprawdzenie wyniku (ręcznie)

```bash
python3 register.py check
```

## Jak działa wyszukiwanie (search_items)

1. **Keyword prefilter** — tokenizuje query i porównuje ze słowami w nazwach itemów.
   Zwraca max 200 najlepiej pasujących kandydatów.
2. **LLM matching** — wysyła zawężoną listę do GPT-4.1 z prośbą o semantyczne
   dopasowanie. LLM rozumie synonimy i parametry (np. "akumulator pod 48V"
   → "Akumulator AGM 48V 150Ah").

## Ograniczenia (narzucone przez centrala)

- Odpowiedź narzędzia: 4–500 bajtów
- Agent ma max 10 kroków
- Agent szuka 3 przedmiotów
- Max 2 zarejestrowane narzędzia
