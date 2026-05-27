"""LLM-powered item search using OpenRouter for fuzzy matching.

Uses a two-stage approach:
1. Local keyword pre-filter to narrow down candidates
2. LLM for final semantic matching on the filtered subset
"""

import json
import os
import re

import requests
from dotenv import load_dotenv

from data_store import KnowledgeBase, Item

load_dotenv()

OPENROUTER_API_KEY = os.getenv("OPEN_ROUTER_API_KEY")
OPENROUTER_MODEL = os.getenv("OPEN_ROUTER_MODEL", "openai/gpt-4o-mini")
OPENROUTER_BASE_URL = os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1")

MAX_CANDIDATES = 200


def _normalize(text: str) -> str:
    return re.sub(r"[^a-z0-9ąćęłńóśźż]", " ", text.lower())


def _keyword_prefilter(query: str, items: list[Item]) -> list[Item]:
    """Pre-filter items using keyword matching to reduce the set for LLM."""
    query_normalized = _normalize(query)
    query_tokens = set(query_normalized.split())

    scored = []
    for item in items:
        item_normalized = _normalize(item.name)
        item_tokens = set(item_normalized.split())
        overlap = query_tokens & item_tokens
        if overlap:
            scored.append((len(overlap), item))

    scored.sort(key=lambda x: x[0], reverse=True)
    candidates = [item for _, item in scored[:MAX_CANDIDATES]]

    if not candidates:
        return items[:MAX_CANDIDATES]

    return candidates


def search_items(query: str, kb: KnowledgeBase) -> list[dict]:
    """
    Use LLM to match a natural language query to items in the database.
    Returns list of matching items with their codes and names.
    """
    candidates = _keyword_prefilter(query, kb.items)
    item_list = "\n".join(f"- {item.name} [code: {item.code}]" for item in candidates)

    system_prompt = (
        "You are an assistant that matches user queries to electronic components. "
        "The user describes what they need in natural language (Polish or English). "
        "You must find ALL items from the list that match the query. "
        "Match by function, type, and parameters (voltage, power, size, etc). "
        "Be generous in matching - include items that could fulfill the user's need. "
        "Return ONLY a JSON array of objects with 'code' and 'name' fields. "
        "If nothing matches, return an empty array []. "
        "Do not add any explanation, only valid JSON."
    )

    user_prompt = (
        f"Available items:\n{item_list}\n\n"
        f"User query: \"{query}\"\n\n"
        "Which items match this query? Return JSON array."
    )

    response = requests.post(
        f"{OPENROUTER_BASE_URL}/chat/completions",
        headers={
            "Authorization": f"Bearer {OPENROUTER_API_KEY}",
            "Content-Type": "application/json",
        },
        json={
            "model": OPENROUTER_MODEL,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
            "temperature": 0.0,
        },
        timeout=60,
    )
    response.raise_for_status()

    content = response.json()["choices"][0]["message"]["content"].strip()
    content = content.removeprefix("```json").removeprefix("```").removesuffix("```").strip()

    try:
        results = json.loads(content)
    except json.JSONDecodeError:
        return []

    if not isinstance(results, list):
        return []

    return results
