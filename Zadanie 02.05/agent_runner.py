"""Generyczna pętla agenta z wywołaniami narzędzi."""

from __future__ import annotations

import json
from typing import Any, Callable, Dict, List, Optional

from config import AGENT_MAX_STEPS, OPEN_ROUTER_MODEL
from llm_client import OpenRouterClient

ToolHandler = Callable[..., Dict[str, Any]]
SuccessPredicate = Callable[[str, Dict[str, Any]], bool]


def run_tool_agent(
    *,
    system_prompt: str,
    user_prompt: str,
    tool_definitions: List[Dict[str, Any]],
    tool_map: Dict[str, ToolHandler],
    success_predicate: SuccessPredicate,
    continue_prompt: str,
    model: str = OPEN_ROUTER_MODEL,
    max_steps: int = AGENT_MAX_STEPS,
    llm: Optional[OpenRouterClient] = None,
) -> Optional[Dict[str, Any]]:
    client = llm or OpenRouterClient()
    messages: List[Dict[str, Any]] = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_prompt},
    ]

    for step in range(max_steps):
        print(f"\n--- Krok agenta {step + 1} ---")
        response = client.chat(model, messages, max_tokens=4096, tools=tool_definitions)
        choice = response["choices"][0]
        message = choice["message"]
        messages.append(message)

        finish_reason = choice.get("finish_reason")
        if finish_reason != "tool_calls":
            if finish_reason == "stop":
                content = (message.get("content") or "").strip()
                print(f"  Agent: {content[:300]}")
                messages.append({"role": "user", "content": continue_prompt})
                continue
            print(f"  Nieoczekiwany finish_reason: {finish_reason}")
            break

        final_result = None
        for tool_call in message.get("tool_calls", []):
            fn_name = tool_call["function"]["name"]
            fn_args = json.loads(tool_call["function"]["arguments"])
            print(f"  Narzędzie: {fn_name}({json.dumps(fn_args, ensure_ascii=False)})")

            handler = tool_map.get(fn_name)
            if handler:
                try:
                    result = handler(**fn_args)
                except Exception as exc:
                    result = {"error": str(exc)}
            else:
                result = {"error": f"Nieznane narzędzie: {fn_name}"}

            result_str = json.dumps(result, ensure_ascii=False)
            if success_predicate(fn_name, result):
                print(f"\n  *** SUKCES! {result_str} ***")
                final_result = result
            elif fn_name == "submit_instructions":
                print(f"  WYSYŁKA NIEUDANA — hub: {result_str[:800]}")
            else:
                preview = result_str[:600]
                print(f"  Wynik: {preview}{'...' if len(result_str) > 600 else ''}")

            messages.append({
                "role": "tool",
                "tool_call_id": tool_call["id"],
                "content": result_str,
            })

        if final_result is not None:
            return final_result

    print("Osiągnięto limit kroków agenta.")
    return None
