"""Misja drona — agent planujący i wysyłający instrukcje do huba."""

from __future__ import annotations

from typing import Any, Dict, Optional

from agent_runner import run_tool_agent
from config import MISSION_RESULT_PATH, require_openrouter_key
from drone_tools import TOOL_DEFINITIONS, TOOL_MAP, submit_instructions
from io_utils import save_json
from prompts import AGENT_CONTINUE_PROMPT, MISSION_SYSTEM_PROMPT, MISSION_USER_PROMPT


def _mission_success(tool_name: str, result: Dict[str, Any]) -> bool:
    return tool_name == "submit_instructions" and bool(result.get("success"))


def submit_drone_mission() -> Optional[Dict[str, Any]]:
    """
    Punkt 3 z task.md:
    1. Czyta dokumentację API drona
    2. Na podstawie dokumentacji i sektora tamy identyfikuje wymagane instrukcje (agent LLM)
    3. Wysyła sekwencję instrukcji do endpointu /verify
    """
    require_openrouter_key()

    result = run_tool_agent(
        system_prompt=MISSION_SYSTEM_PROMPT,
        user_prompt=MISSION_USER_PROMPT,
        tool_definitions=TOOL_DEFINITIONS,
        tool_map=TOOL_MAP,
        success_predicate=_mission_success,
        continue_prompt=AGENT_CONTINUE_PROMPT,
    )

    if result:
        save_json(MISSION_RESULT_PATH, result)
        print(f"[OK]  Zapisano wynik misji: {MISSION_RESULT_PATH}")
    return result


__all__ = ["submit_drone_mission", "submit_instructions"]
