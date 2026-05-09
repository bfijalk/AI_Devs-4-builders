import json

from findhim.config import MODEL, get_llm_client
from findhim.tools import ALL_TOOLS, TOOL_MAP
from findhim.verify import ANSWER_SCHEMA

MAX_STEPS = 20

_client = get_llm_client()


def run_agent(user_message: str) -> dict:
    messages = [{"role": "user", "content": user_message}]
    step = 0

    while True:
        response = _client.chat.completions.create(
            model=MODEL,
            messages=messages,
            tools=ALL_TOOLS,
        )
        msg = response.choices[0].message
        messages.append(msg)

        if response.choices[0].finish_reason == "stop":
            structured = _client.chat.completions.create(
                model=MODEL,
                messages=messages + [{
                    "role": "user",
                    "content": (
                        "Na podstawie zebranych danych zwróć wyłącznie obiekt JSON z polami: "
                        "name (imię), surname (nazwisko), accessLevel (poziom dostępu jako liczba), "
                        "powerPlant (KOD elektrowni w formacie PWRxxxxPL, np. PWR3847PL — NIE nazwa miasta)."
                    ),
                }],
                response_format=ANSWER_SCHEMA,
            )
            return json.loads(structured.choices[0].message.content)

        if step >= MAX_STEPS:
            raise RuntimeError(f"Agent przekroczył limit {MAX_STEPS} kroków bez odpowiedzi.")

        for tool_call in msg.tool_calls:
            args = json.loads(tool_call.function.arguments)
            print(f"[krok {step + 1}] narzędzie: {tool_call.function.name}, parametry: {json.dumps(args, ensure_ascii=False)}")
            result = TOOL_MAP[tool_call.function.name](args)
            print(f"          wynik: {json.dumps(result, ensure_ascii=False)[:200]}")
            messages.append({
                "role": "tool",
                "tool_call_id": tool_call.id,
                "content": json.dumps(result, ensure_ascii=False),
            })
            step += 1
