"""Punkt 4 (część 2): analiza LLM notatek operatora."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Dict, List, Optional

from config import INVALID_LLM_PATH, LLM_BATCH_SIZE, OPEN_ROUTER_MODEL, OPERATOR_NOTES_PATH
from llm_client import OpenRouterClient


SYSTEM_PROMPT = (
    "You analyze operator notes from industrial sensor readings. "
    "Each entry maps file_id to operator_notes. "
    "Flag file_id ONLY when the operator reports a REAL problem or orders follow-up action.\n\n"
    "FLAG when the operator:\n"
    "- says readings are suspicious, concerning, unstable, anomalous, irregular, unreliable, "
    "inconsistent, unhealthy, doubtful, or not normal\n"
    "- orders escalation, investigation, audit, diagnostics, revalidation, maintenance, "
    "troubleshooting, replacement, on-site inspection, or root-cause analysis\n"
    "- uses actions like: escalated, flagged, submitted for analysis, opened diagnostic task, "
    "marked for revalidation, placed under investigation\n\n"
    "DO NOT FLAG when the operator approves the reading, for example:\n"
    "- 'no warning signs appeared', 'standard pass', 'without action', 'approved as-is'\n"
    "- 'confirmed regular operation', 'left the setup untouched', 'no corrective steps'\n"
    "- 'healthy', 'stable', 'nominal', 'clean', 'solid', 'reassuring', 'trustworthy'\n"
    "- 'no deviations', 'no intervention', 'no escalation', 'closed this check'\n\n"
    "IMPORTANT: 'No warning signs appeared' means everything is OK — do NOT flag it.\n"
    "IMPORTANT: Focus on the operator's CONCLUSION and ACTION at the end of the note.\n"
    "If the note approves or closes without action, do NOT flag even if diagnostic words appear.\n\n"
    "Examples:\n"
    "- FLAG: 'These readings look suspicious, so I escalated for engineering analysis'\n"
    "- DO NOT FLAG: 'No warning signs appeared, readings align with normal patterns, "
    "I recorded a standard pass'\n"
    "- DO NOT FLAG: 'Performance appears nominal, I closed this check without action'\n\n"
    "Return ONLY a JSON array of file_id strings. No .json suffix. If none match, return []."
)

APPROVAL_PHRASES = (
    "without action",
    "approved as-is",
    "approved the report",
    "standard pass",
    "no warning signs appeared",
    "no warning signs",
    "no deviations",
    "no corrective",
    "no intervention",
    "no escalation",
    "confirmed regular operation",
    "left the setup untouched",
    "closed this check",
    "marked the cycle as healthy",
    "signed off",
    "recorded a standard pass",
)

ESCALATION_PHRASES = (
    "escalated",
    "flagged",
    "quality audit",
    "root-cause",
    "diagnostic task",
    "troubleshoot",
    "revalidation",
    "replacement assessment",
    "maintenance follow-up",
    "under investigation",
    "anomaly check",
    "on-site inspection",
    "probable fault",
    "engineering analysis",
    "urgent verification",
    "focused technical review",
    "deeper diagnostic",
    "submitted it for root-cause",
    "opened a deeper diagnostic",
    "marked this case for revalidation",
    "placed the unit under investigation",
    "requested a focused technical review",
)


def _note_confirms_approval(notes: str) -> bool:
    """Deterministyczny filtr po LLM — odrzuca wyraźnie pozytywne notatki."""
    lowered = notes.lower()
    has_approval = any(phrase in lowered for phrase in APPROVAL_PHRASES)
    has_escalation = any(phrase in lowered for phrase in ESCALATION_PHRASES)
    return has_approval and not has_escalation


def _filter_false_positives(flagged_ids: List[str], notes: Dict[str, str]) -> List[str]:
    return sorted(
        file_id
        for file_id in flagged_ids
        if file_id in notes and not _note_confirms_approval(notes[file_id])
    )


def load_operator_notes(path: Optional[Path] = None) -> Dict[str, str]:
    source = path or OPERATOR_NOTES_PATH
    if not source.exists():
        raise FileNotFoundError(f"Nie znaleziono pliku z notatkami operatora: {source}")
    return json.loads(source.read_text(encoding="utf-8"))


def _parse_llm_ids(content: str) -> List[str]:
    fenced = re.search(r"```(?:json)?\s*(\[[\s\S]*?\])\s*```", content)
    raw = fenced.group(1) if fenced else content.strip()

    start = raw.find("[")
    end = raw.rfind("]")
    if start == -1 or end == -1:
        return []

    parsed = json.loads(raw[start : end + 1])
    if not isinstance(parsed, list):
        return []

    return [Path(str(item).strip()).stem for item in parsed if str(item).strip()]


def _analyze_batch(client: OpenRouterClient, batch: Dict[str, str]) -> List[str]:
    payload = json.dumps(batch, ensure_ascii=False)
    content = client.chat_text(
        OPEN_ROUTER_MODEL,
        [
            {"role": "system", "content": SYSTEM_PROMPT},
            {
                "role": "user",
                "content": (
                    "Which file_id entries have operator notes reporting a problem?\n\n"
                    f"{payload}"
                ),
            },
        ],
        max_tokens=8000,
    )
    return _parse_llm_ids(content)


def analyze_operator_notes(
    notes_path: Optional[Path] = None,
    output_path: Optional[Path] = None,
    batch_size: int = LLM_BATCH_SIZE,
) -> List[str]:
    """
    Analizuje notatki operatora i zapisuje numery plików z błędami do niepoprawne_llm.
    Dla pełnego zbioru (9999 plików) używa batchy, bo jeden request przekracza limit modelu.
    """
    notes = load_operator_notes(notes_path)
    client = OpenRouterClient()
    flagged_ids: List[str] = []

    items = list(notes.items())
    total_batches = (len(items) + batch_size - 1) // batch_size

    print(f"[LLM]  Analyzing {len(notes)} operator notes in {total_batches} batch(es)...")
    for batch_index in range(total_batches):
        start = batch_index * batch_size
        batch = dict(items[start : start + batch_size])
        print(f"[LLM]  Batch {batch_index + 1}/{total_batches} ({len(batch)} notes)...")
        flagged_ids.extend(_analyze_batch(client, batch))

    unique_ids = _filter_false_positives(sorted(set(flagged_ids)), notes)
    removed = len(set(flagged_ids)) - len(unique_ids)
    if removed:
        print(f"[OK]  Removed {removed} false positive(s) after approval filter")

    target = output_path or INVALID_LLM_PATH
    target.write_text("\n".join(unique_ids) + ("\n" if unique_ids else ""), encoding="utf-8")

    print(f"[OK]  LLM flagged {len(unique_ids)} files")
    print(f"[OK]  Saved to {target}")
    return unique_ids


if __name__ == "__main__":
    analyze_operator_notes()
