# OKO Editor API

Endpoint: `POST https://hub.ag3nts.org/verify`

Task: `okoeditor`

Available commands and request syntax for okoeditor API.

## `help`

### Składnia żądania

```json
{
  "apikey": "YOUR_API_KEY",
  "task": "okoeditor",
  "answer": {
    "action": "help"
  }
}
```

### Uwagi

- Returns this help message.
- No additional fields are required.

## `update`

### Składnia żądania

```json
{
  "apikey": "YOUR_API_KEY",
  "task": "okoeditor",
  "answer": {
    "page": "incydenty|notatki|zadania",
    "id": "32-char-hex-id",
    "action": "update",
    "content": "new description text (optional)",
    "title": "new title (optional)",
    "done": "YES|NO (only for page zadania, optional)"
  }
}
```

### Pola wymagane

- page
- id
- action

### Pola opcjonalne

- content
- title
- done

### Zasady

- At least one of "content" or "title" must be provided.
- "done" is allowed only for page "zadania".
- Page "uzytkownicy" is read-only and cannot be updated.

## `done`

### Składnia żądania

```json
{
  "apikey": "YOUR_API_KEY",
  "task": "okoeditor",
  "answer": {
    "action": "done"
  }
}
```

### Uwagi

- Verifies if all required data edits are completed.
- Returns a flag only when every condition is satisfied.
