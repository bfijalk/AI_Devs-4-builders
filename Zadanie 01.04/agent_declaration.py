import json
import os

from openai import OpenAI
from dotenv import load_dotenv

import log
import tools_declaration as tools2

load_dotenv()

_client = OpenAI(
    api_key=os.getenv("OPEN_ROUTER_API_KEY"),
    base_url=os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1"),
)

MODEL = os.getenv("OPEN_ROUTER_MODEL", "gpt-4o")
MAX_STEPS = 30

SYSTEM_PROMPT = """Jesteś agentem systemu SPK (System Przesyłek Konduktorskich).
Twoim zadaniem jest wypełnienie deklaracji zawartości przesyłki i wysłanie jej do weryfikacji.

Masz dostęp do następujących narzędzi:
- read_file: wczytuje pliki z dokumentacją z folderu files/
- find_route: wyznacza trasę między dwoma miastami w sieci SPK
- calculate_fee: oblicza opłatę wg regulaminu SPK
- fill_declaration: wypełnia wzór deklaracji i zapisuje go
- send_to_verify: wysyła gotową deklarację na endpoint /verify

Sekwencja działań:
1. Przeczytaj dokumentację (index.md) aby poznać trasy i zasady.
2. Przeczytaj wzór deklaracji (zalacznik-E.md).
3. Wyznacz trasę dla podanej przesyłki za pomocą find_route.
4. Wybierz kategorię przesyłki tak, aby KWOTA DO ZAPŁATY wynosiła 0 PP.
   Kategorie A i B są zwolnione z opłat — finansowane przez System.
   Użyj calculate_fee, aby to potwierdzić.
5. Wypełnij deklarację za pomocą fill_declaration.
6. Wyślij deklarację na /verify za pomocą send_to_verify.

Ważne zasady:
- Budżet = 0 PP — wybierz kategorię A lub B (zwolnione z opłat).
- Zachowaj oryginalny format deklaracji z zalacznik-E.md.
- Kod trasy: użyj kodu zwróconego przez find_route (np. X-01).
- Data: użyj dzisiejszej daty w formacie YYYY-MM-DD.
- WDP = 0 (brak wagonów dodatkowych).
- Trasy wyłączone (X-01..X-08) są dostępne WYŁĄCZNIE dla przesyłek kategorii A i B.
  Jeśli trasa do celu prowadzi przez strefę wyłączoną, wymagana jest kategoria A lub B.
- Pole WDP: użyj wartości `wdp` zwróconej przez calculate_fee.
  Standardowy skład = 2 wagony × 500 kg = 1000 kg. Każde dodatkowe 500 kg (lub część) = 1 WDP.
  Wzór: WDP = max(0, ceil(masa_kg / 500) - 2). Dla 2800 kg = ceil(2800/500)-2 = 6-2 = 4.
"""


def run(shipment: dict) -> str:
    """
    Uruchamia agentyczną pętlę dla przesyłki opisanej słownikiem shipment.
    Oczekiwane klucze: origin, destination, sender_id, weight_kg, description, notes.
    """
    user_message = (
        f"Wypełnij deklarację SPK dla następującej przesyłki:\n"
        f"- Punkt nadawczy: {shipment.get('origin', 'Warszawa')}\n"
        f"- Punkt docelowy: {shipment.get('destination', 'Kraków')}\n"
        f"- Nadawca (ID): {shipment.get('sender_id', 'ID-2041-WW')}\n"
        f"- Masa: {shipment.get('weight_kg', 2)} kg\n"
        f"- Opis zawartości: {shipment.get('description', 'Dokumenty urzędowe')}\n"
        f"- Uwagi specjalne: {shipment.get('notes', 'Brak')}\n\n"
        "Budżet wynosi 0 PP — wybierz kategorię finansowaną przez System. "
        "Po wypełnieniu deklaracji wyślij ją na /verify."
    )

    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "user", "content": user_message},
    ]

    for step in range(1, MAX_STEPS + 1):
        log.ai_call(len(messages))

        response = _client.chat.completions.create(
            model=MODEL,
            messages=messages,
            tools=tools2.DEFINITIONS,
            tool_choice="auto",
        )

        message = response.choices[0].message
        finish_reason = response.choices[0].finish_reason

        if finish_reason == "tool_calls":
            messages.append(message)
            for tool_call in message.tool_calls:
                name = tool_call.function.name
                args = json.loads(tool_call.function.arguments)
                handler = tools2.HANDLERS.get(name)
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
