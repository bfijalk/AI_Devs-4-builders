"""Tools for interacting with the zmail API."""

import os
from typing import Union
import requests
from dotenv import load_dotenv

load_dotenv()

ZMAIL_URL = "https://hub.ag3nts.org/api/zmail"
API_KEY = os.getenv("API_KEY")


def _call(payload: dict) -> dict:
    payload["apikey"] = API_KEY
    resp = requests.post(ZMAIL_URL, json=payload, timeout=30)
    resp.raise_for_status()
    return resp.json()


def get_inbox(page: int = 1, per_page: int = 10) -> dict:
    """Return list of threads in the mailbox."""
    return _call({"action": "getInbox", "page": page, "perPage": per_page})


def get_thread(thread_id: int) -> dict:
    """Return rowID and messageID list for a selected thread (no message body)."""
    return _call({"action": "getThread", "threadID": thread_id})


def get_messages(ids: Union[list, int, str]) -> dict:
    """Return one or more messages by rowID or messageID (hash)."""
    return _call({"action": "getMessages", "ids": ids})


def search(query: str, page: int = 1, per_page: int = 10) -> dict:
    """Search messages using Gmail-like operators (from:, to:, subject:, phrases, -exclude, OR, AND)."""
    return _call({"action": "search", "query": query, "page": page, "perPage": per_page})


TOOL_DEFINITIONS = [
    {
        "type": "function",
        "function": {
            "name": "get_inbox",
            "description": "Return list of email threads in the mailbox.",
            "parameters": {
                "type": "object",
                "properties": {
                    "page": {"type": "integer", "description": "Page number, default 1"},
                    "per_page": {"type": "integer", "description": "Results per page (5-20), default 10"},
                },
                "required": [],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_thread",
            "description": "Return rowID and messageID list for a selected thread. No message body.",
            "parameters": {
                "type": "object",
                "properties": {
                    "thread_id": {"type": "integer", "description": "Numeric thread identifier"},
                },
                "required": ["thread_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_messages",
            "description": "Return one or more messages by rowID or messageID hash.",
            "parameters": {
                "type": "object",
                "properties": {
                    "ids": {
                        "description": "Numeric rowID, 32-char messageID, or an array of them",
                        "oneOf": [
                            {"type": "integer"},
                            {"type": "string"},
                            {"type": "array", "items": {"oneOf": [{"type": "integer"}, {"type": "string"}]}},
                        ],
                    },
                },
                "required": ["ids"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "search",
            "description": (
                "Search messages with full-text query and Gmail-like operators: "
                "words, \"phrase\", -exclude, from:, to:, subject:, OR, AND."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "query": {"type": "string", "description": "Search query"},
                    "page": {"type": "integer", "description": "Page number, default 1"},
                    "per_page": {"type": "integer", "description": "Results per page (5-20), default 10"},
                },
                "required": ["query"],
            },
        },
    },
]

VERIFY_URL = "https://hub.ag3nts.org/verify"


def submit_answer(password: str, date: str, confirmation_code: str) -> dict:
    """Submit the found password, date, and confirmation_code to the verification endpoint."""
    resp = requests.post(
        VERIFY_URL,
        json={
            "apikey": API_KEY,
            "task": "mailbox",
            "answer": {
                "password": password,
                "date": date,
                "confirmation_code": confirmation_code,
            },
        },
        timeout=30,
    )
    try:
        body = resp.json()
    except Exception:
        body = {"raw": resp.text}

    if not resp.ok:
        return {
            "success": False,
            "status_code": resp.status_code,
            "hub_feedback": body,
            "submitted": {"password": password, "date": date, "confirmation_code": confirmation_code},
            "instruction": (
                "Submission failed. Use hub_feedback to identify which value(s) are wrong. "
                "Keep searching the mailbox - try different search queries, read unread messages, "
                "and use wait() if you need new emails to arrive. Try again with corrected values."
            ),
        }
    return {"success": True, "flag": body}


def wait(seconds: int = 10) -> dict:
    """Wait for a number of seconds before continuing (useful when waiting for new emails to arrive)."""
    import time
    seconds = max(5, min(seconds, 60))
    time.sleep(seconds)
    return {"status": "waited", "seconds": seconds, "message": "Done waiting. New emails may have arrived. Try searching again."}


def finish(reason: str) -> dict:
    """Signal that the agent has completed its task. Only call this after a SUCCESSFUL submit_answer."""
    return {"status": "finished", "reason": reason}


TOOL_DEFINITIONS = TOOL_DEFINITIONS + [
    {
        "type": "function",
        "function": {
            "name": "submit_answer",
            "description": (
                "Submit the discovered password, date, and confirmation_code to the hub for verification. "
                "Call this when you have found all three values."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "password": {"type": "string", "description": "The password found in the mailbox"},
                    "date": {"type": "string", "description": "The date in YYYY-MM-DD format"},
                    "confirmation_code": {"type": "string", "description": "The confirmation code starting with SEC-"},
                },
                "required": ["password", "date", "confirmation_code"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "wait",
            "description": "Wait for a number of seconds before continuing. Use this when you need to wait for new emails to arrive in the active mailbox.",
            "parameters": {
                "type": "object",
                "properties": {
                    "seconds": {"type": "integer", "description": "How many seconds to wait (5-120), default 30"},
                },
                "required": [],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "finish",
            "description": "Signal that the agent has completed its task. ONLY call this after a SUCCESSFUL submit_answer (success=true).",
            "parameters": {
                "type": "object",
                "properties": {
                    "reason": {"type": "string", "description": "Reason for finishing"},
                },
                "required": ["reason"],
            },
        },
    },
]

TOOL_MAP = {
    "get_inbox": get_inbox,
    "get_thread": get_thread,
    "get_messages": get_messages,
    "search": search,
    "submit_answer": submit_answer,
    "wait": wait,
    "finish": finish,
}
