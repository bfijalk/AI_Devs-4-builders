# Zadanie 02.04 — Agent mailowy (zmail)

## Cel

Zadanie polega na zbudowaniu agenta LLM, który autonomicznie przeszukuje skrzynkę mailową (`zmail`) i odnajduje trzy ukryte wartości:

- **hasło** (`password`) — np. z e-maila o zmianie hasła do systemu
- **datę** (`date`) — konkretna data w formacie `YYYY-MM-DD` wymieniona w treści wiadomości
- **kod potwierdzenia** (`confirmation_code`) — zaczyna się od `SEC-`

Po zebraniu wszystkich trzech wartości agent wysyła je do endpointu `/verify`. Gdy są poprawne, serwer zwraca flagę `{FLG:...}`.

## Architektura

```
agent.py          →  pętla agentowa (ReAct loop)
zmail_tools.py    →  narzędzia Python + definicje Function Calling
.env              →  klucze API
```

### `zmail_tools.py` — narzędzia agenta

Każde narzędzie to funkcja Python opatrzona definicją JSON (format OpenAI Function Calling). Agent LLM może je wywoływać autonomicznie.

| Narzędzie | Opis |
|---|---|
| `get_inbox(page, per_page)` | Lista wątków w skrzynce (z paginacją) |
| `get_thread(thread_id)` | Lista ID wiadomości w wątku (bez treści) |
| `get_messages(ids)` | Pełna treść jednej lub wielu wiadomości (po rowID lub 32-znakowym messageID) |
| `search(query, page, per_page)` | Wyszukiwanie operatorami Gmail: `from:`, `to:`, `subject:`, `"fraza"`, `-exclude`, `OR`, `AND` |
| `submit_answer(password, date, confirmation_code)` | Wysyła odpowiedź do `/verify`. Zwraca `success=True` + flagę, albo `success=False` + feedback huba |
| `wait(seconds)` | Czeka 5–60 sekund na nowe wiadomości |
| `finish(reason)` | Kończy pętlę — tylko po udanym `submit_answer` |

### `agent.py` — pętla agentowa

Implementuje klasyczną pętlę ReAct (Reason + Act):

1. LLM dostaje system prompt + historię konwersacji + definicje narzędzi
2. LLM decyduje, które narzędzie wywołać
3. Python wykonuje narzędzie i zwraca wynik do LLM
4. Pętla powtarza się aż do sukcesu (maks. 100 kroków)

```
Inicjalizacja konwersacji
        ↓
   Wywołaj LLM
        ↓
  finish_reason == "tool_calls"?
        ↓ TAK
  Wykonaj narzędzie
        ↓
  submit_answer → success=True?
     TAK → koniec
     NIE → kontynuuj pętlę
        ↓
  finish_reason == "stop"?
     → Nakłoń agenta do dalszego działania
```

### Kluczowe zachowania

- **Feedback huba** — po nieudanym `submit_answer` agent otrzymuje pełną odpowiedź serwera i może wywnioskować, która wartość jest błędna
- **Aktywna skrzynka** — nowe wiadomości mogą nadejść w trakcie działania agenta; `wait(10)` + ponowne wyszukiwanie zapewnia, że agent ich nie przegapi
- **Brak przedwczesnego zakończenia** — `finish()` może zostać wywołane tylko po `success=True`; błąd submisji nie przerywa pętli

## Uruchomienie

```bash
cd "Zadanie 02.04"
pip install requests python-dotenv
python3 agent.py
```

## Wymagany `.env`

```env
API_KEY=twoj-klucz-do-huba
OPEN_ROUTER_API_KEY=sk-or-...
OPEN_ROUTER_MODEL=openai/gpt-4o-mini
OPEN_ROUTER_BASE_URL=https://openrouter.ai/api/v1
```

## API zmail

```
POST https://hub.ag3nts.org/api/zmail
Content-Type: application/json

{ "apikey": "...", "action": "getInbox", "page": 1 }
```

Tryb: **read-only**. Dostępne akcje: `help`, `getInbox`, `getThread`, `getMessages`, `search`, `reset`.

## Weryfikacja odpowiedzi

```
POST https://hub.ag3nts.org/verify

{
  "apikey": "...",
  "task": "mailbox",
  "answer": {
    "password": "znalezione-haslo",
    "date": "2026-MM-DD",
    "confirmation_code": "SEC-..."
  }
}
```
