import json
import os

from openai import OpenAI
from dotenv import load_dotenv

import log
import tools_fetcher as tools

load_dotenv()

_client = OpenAI(
    api_key=os.getenv("OPEN_ROUTER_API_KEY"),
    base_url=os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1"),
)

MODEL = os.getenv("OPEN_ROUTER_MODEL", "gpt-4o")
MAX_STEPS = 20

SYSTEM_PROMPT = """Jesteś pomocnym agentem do zarządzania dokumentacją.
Masz dostęp do dwóch narzędzi:

1. fetch_docs(url) — pobiera plik z internetu oraz rekurencyjnie wszystkie pliki, do których się odwołuje.
   Zapisuje je lokalnie w folderze files/. Użyj gdy użytkownik prosi o pobranie dokumentacji.

2. image_to_docs(image_path) — analizuje plik graficzny modelem wizyjnym i generuje dokumentację Markdown.
   Wynikowy plik .md jest zapisywany obok oryginału. Użyj gdy użytkownik prosi o opisanie lub udokumentowanie obrazu.

Po wykonaniu zadania poinformuj użytkownika o wynikach."""


def run(user_message: str) -> str:
    """Uruchamia agentyczną pętlę i zwraca końcową odpowiedź."""
    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "user", "content": user_message},
    ]

    for step in range(1, MAX_STEPS + 1):
        log.ai_call(len(messages))

        response = _client.chat.completions.create(
            model=MODEL,
            messages=messages,
            tools=tools.DEFINITIONS,
            tool_choice="auto",
        )

        message = response.choices[0].message
        finish_reason = response.choices[0].finish_reason

        if finish_reason == "tool_calls":
            messages.append(message)
            for tool_call in message.tool_calls:
                name = tool_call.function.name
                args = json.loads(tool_call.function.arguments)
                handler = tools.HANDLERS.get(name)
                if handler:
                    result = handler(args)
                else:
                    result = {"error": f"Nieznane narzędzie: {name}"}
                messages.append({
                    "role": "tool",
                    "tool_call_id": tool_call.id,
                    "content": json.dumps(result, ensure_ascii=False),
                })
            continue

        reply = message.content or ""
        log.ai_response(reply)
        return reply

    raise RuntimeError(f"Agent nie zakończył działania w ciągu {MAX_STEPS} kroków.")
