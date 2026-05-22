import json
import logging
import os
from typing import Optional

from openai import OpenAI
from dotenv import load_dotenv

import tools_railway as tools

load_dotenv()

_client = OpenAI(
    api_key=os.getenv("OPEN_ROUTER_API_KEY"),
    base_url=os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1"),
)

MODEL = os.getenv("OPEN_ROUTER_MODEL", "gpt-4o")
MAX_STEPS = 30

SYSTEM_PROMPT = """Jesteś agentem zarządzającym trasami kolejowymi przez API railway.

Masz dostęp do następujących narzędzi:
- help: wyświetla dostępne akcje
- getstatus(route): pobiera aktualny status trasy
- reconfigure(route): włącza tryb rekonfiguracji trasy
- setstatus(route, value): ustawia status trasy (RTOPEN lub RTCLOSE) — wymaga wcześniej reconfigure
- save(route): zapisuje zmiany i wychodzi z trybu rekonfiguracji
- activate_route(route): automatycznie wykonuje pełną sekwencję aktywacji trasy (getstatus → reconfigure → setstatus RTOPEN → save)

Jeśli użytkownik chce aktywować lub otworzyć trasę, użyj narzędzia activate_route — samo zadecyduje, które kroki są potrzebne.
Narzędzie activate_route pamięta postęp — przy ponownym wywołaniu dla tej samej trasy wznowi sekwencję od nieudanego kroku.
Jeśli chce tylko sprawdzić status lub wykonać konkretną operację, użyj odpowiedniego narzędzia bezpośrednio.

WAŻNE: Jeśli narzędzie zwróci błąd, NIE ponawiaj samodzielnie. System przerwie działanie i zapyta użytkownika o decyzję.
Po zakończeniu poinformuj o wyniku."""


def run(user_message: str, history: Optional[list] = None) -> tuple:
    """
    Uruchamia agentyczną pętlę.
    Zwraca (odpowiedź, zaktualizowana historia).
    Jeśli akcja zakończy się błędem, zwraca opis błędu i prosi o decyzję użytkownika.
    """
    messages = history or [{"role": "system", "content": SYSTEM_PROMPT}]
    messages.append({"role": "user", "content": user_message})

    for _ in range(MAX_STEPS):
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
                    logging.info("Narzędzie: %s\n%s", name, json.dumps(args, ensure_ascii=False, indent=2))
                    try:
                        result = handler(args)
                    except Exception as exc:
                        logging.error("Błąd narzędzia %s: %s", name, exc)
                        result = {"error": str(exc)}
                else:
                    result = {"error": f"Nieznane narzędzie: {name}"}

                if "error" in result or (isinstance(result, dict) and result.get("code", 0) < 0):
                    messages.append({
                        "role": "tool",
                        "tool_call_id": tool_call.id,
                        "content": json.dumps(result, ensure_ascii=False),
                    })

                    failed_step = result.get("error") or f"kod {result.get('code')}"
                    details = result.get("details") or {}
                    api_message = details.get("message") or result.get("message") or ""
                    api_code = details.get("code") or result.get("code") or ""
                    last_step = ""
                    if result.get("steps"):
                        last_step = result["steps"][-1].get("step", "")

                    parts = [f"Napotkałem problem podczas wykonywania akcji `{name}`."]
                    if last_step:
                        parts.append(f"Sekwencja zatrzymała się na kroku: `{last_step}`.")
                    parts.append(f"Przyczyna błędu: {failed_step}")
                    if api_code:
                        parts.append(f"Kod błędu API: {api_code}")
                    if api_message:
                        parts.append(f"Komunikat API: {api_message}")
                    parts.append("\nPostęp został zapamiętany. Czy chcesz spróbować ponownie od tego miejsca, czy zmienić instrukcje?")

                    error_reply = "\n".join(parts)
                    messages.append({"role": "assistant", "content": error_reply})
                    return error_reply, messages
                else:
                    messages.append({
                        "role": "tool",
                        "tool_call_id": tool_call.id,
                        "content": json.dumps(result, ensure_ascii=False),
                    })
            continue

        reply = message.content or ""
        messages.append({"role": "assistant", "content": reply})
        return reply, messages

    raise RuntimeError(f"Agent nie zakończył działania w ciągu {MAX_STEPS} kroków.")
