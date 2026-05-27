"""LLM agent that searches the zmail mailbox to find password, date, and confirmation_code."""

import json
import os
from typing import Optional
import requests
from dotenv import load_dotenv

from zmail_tools import TOOL_DEFINITIONS, TOOL_MAP

load_dotenv()

OPENROUTER_API_KEY = os.getenv("OPEN_ROUTER_API_KEY")
OPENROUTER_MODEL = os.getenv("OPEN_ROUTER_MODEL", "openai/gpt-4o-mini")
OPENROUTER_BASE_URL = os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1")

MAX_STEPS = 100

SYSTEM_PROMPT = """You are an email detective agent working with an ACTIVE, live mailbox.

YOUR GOAL: Find three values and submit them via submit_answer until the hub returns a flag (success=true).
1. PASSWORD - e.g. found in an email about "nowe hasło", "hasło do systemu"
2. DATE - a specific date in YYYY-MM-DD format explicitly mentioned in an email body
3. CONFIRMATION CODE - starts with "SEC-" (e.g. SEC-c1e598764329cc9c377ef1d029be8ceb)

HOW TO WORK:
- Search for each value SEPARATELY using search() with different queries
- Read FULL message content with get_messages() - the answer is in the body text, not just the subject
- For threads with multiple messages: use get_thread() to list all message IDs, then get_messages() with an array of IDs to batch-read them all
- After submitting, READ the hub_feedback carefully - it may tell you which value is wrong
- If a value is wrong, search more specifically for that value
- The mailbox is LIVE - new emails arrive constantly. Use wait(10) then search again if stuck
- NEVER call finish() before submit_answer returns success=true

SEARCH QUERIES TO USE:
- For password: "hasło", "nowe hasło", "login", "dostęp"  
- For SEC code: "SEC-", "kod potwierdzenia", "confirmation", "ticket"
- For date: "data", "termin", "spotkanie", "2026-", "zaplanowano", "planowany"

IMPORTANT about the DATE:
- The date is explicitly written in some email body (not just the email's send timestamp)
- It may look like "2026-03-XX" or be written in text form
- Check ALL messages in the thread about SEC codes - the date may be there
- Read messages you haven't read yet - especially any unread messages in relevant threads

After each failed submit_answer, analyze hub_feedback and adjust your search.
Continue indefinitely until you succeed."""


def call_llm(messages: list) -> dict:
    resp = requests.post(
        f"{OPENROUTER_BASE_URL}/chat/completions",
        headers={
            "Authorization": f"Bearer {OPENROUTER_API_KEY}",
            "Content-Type": "application/json",
        },
        json={
            "model": OPENROUTER_MODEL,
            "messages": messages,
            "tools": TOOL_DEFINITIONS,
            "tool_choice": "auto",
            "temperature": 0.0,
        },
        timeout=120,
    )
    resp.raise_for_status()
    return resp.json()


def run_agent() -> Optional[dict]:
    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {
            "role": "user",
            "content": (
                "Start now. Search the mailbox for password, date, and confirmation_code. "
                "Search for each value one at a time. Read full message bodies. "
                "For threads with multiple messages, batch-read all of them. "
                "Try submit_answer once you have candidates. If it fails, use hub_feedback to fix values. "
                "Use wait(10) if you need new emails to arrive. Keep going until success."
            ),
        },
    ]

    for step in range(MAX_STEPS):
        print(f"\n--- Step {step + 1} ---")
        response = call_llm(messages)
        choice = response["choices"][0]
        message = choice["message"]
        messages.append(message)

        finish_reason = choice.get("finish_reason")

        if finish_reason == "tool_calls":
            tool_calls = message.get("tool_calls", [])
            should_stop = False
            final_result = None

            for tc in tool_calls:
                fn_name = tc["function"]["name"]
                fn_args = json.loads(tc["function"]["arguments"])
                print(f"  Tool: {fn_name}({json.dumps(fn_args, ensure_ascii=False)})")

                fn = TOOL_MAP.get(fn_name)
                if fn:
                    try:
                        result = fn(**fn_args)
                        result_str = json.dumps(result, ensure_ascii=False)

                        if fn_name == "submit_answer":
                            if result.get("success"):
                                print(f"\n  *** SUCCESS! Flag: {result_str} ***")
                                final_result = {"args": fn_args, "response": result}
                                should_stop = True
                            else:
                                print(f"  SUBMIT FAILED - hub says: {result_str}")
                        elif fn_name == "finish":
                            print(f"  FINISH: {result_str}")
                            should_stop = True
                        elif fn_name == "wait":
                            print(f"  Waited {fn_args.get('seconds', 10)}s")
                        else:
                            preview = result_str[:600]
                            print(f"  Result: {preview}{'...' if len(result_str) > 600 else ''}")

                    except Exception as e:
                        result_str = json.dumps({"error": str(e)})
                        print(f"  Error: {result_str}")
                else:
                    result_str = json.dumps({"error": f"Unknown tool: {fn_name}"})

                messages.append({
                    "role": "tool",
                    "tool_call_id": tc["id"],
                    "content": result_str,
                })

            if should_stop:
                return final_result

        elif finish_reason == "stop":
            content = message.get("content", "").strip()
            print(f"  Agent text: {content[:300]}")
            messages.append({
                "role": "user",
                "content": (
                    "Keep going - use the tools! Search more, read unread messages, "
                    "use wait(10) if needed, then try submit_answer again. "
                    "Do NOT stop until hub returns success=true."
                ),
            })

        else:
            print(f"  Unexpected finish_reason: {finish_reason}")
            break

    print("Max steps reached.")
    return None


if __name__ == "__main__":
    print("Starting email detective agent...")
    result = run_agent()

    if result:
        print(f"\nAgent completed successfully!")
        print(f"Submitted: {json.dumps(result.get('args'), indent=2, ensure_ascii=False)}")
        print(f"Hub response: {json.dumps(result.get('response'), indent=2, ensure_ascii=False)}")
    else:
        print("Agent did not complete successfully.")
