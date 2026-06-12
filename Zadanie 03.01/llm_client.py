"""Klient OpenRouter do analizy notatek operatora."""

from __future__ import annotations

import json
from typing import Any, Dict, List, Optional

import requests

from config import HTTP_TIMEOUT, OPEN_ROUTER_BASE_URL, require_openrouter_key


class OpenRouterClient:
    def __init__(self, api_key: Optional[str] = None, base_url: str = OPEN_ROUTER_BASE_URL):
        self.api_key = api_key or require_openrouter_key()
        self.base_url = base_url.rstrip("/")

    def chat(
        self,
        model: str,
        messages: List[Dict[str, Any]],
        *,
        max_tokens: int = 4000,
        temperature: float = 0.0,
    ) -> Dict[str, Any]:
        response = requests.post(
            f"{self.base_url}/chat/completions",
            headers={
                "Authorization": f"Bearer {self.api_key}",
                "Content-Type": "application/json",
            },
            json={
                "model": model,
                "messages": messages,
                "temperature": temperature,
                "max_tokens": max_tokens,
            },
            timeout=HTTP_TIMEOUT,
        )
        response.raise_for_status()
        return response.json()

    def chat_text(
        self,
        model: str,
        messages: List[Dict[str, Any]],
        *,
        max_tokens: int = 4000,
    ) -> str:
        data = self.chat(model, messages, max_tokens=max_tokens)
        content = data["choices"][0]["message"]["content"]
        if not content:
            raise ValueError(f"Model {model} zwrócił pustą odpowiedź")
        return content.strip()
