import json
import threading
from pathlib import Path

import log

SESSIONS_DIR = Path(__file__).parent / "sessions"
SESSIONS_DIR.mkdir(exist_ok=True)

_cache: dict[str, list[dict]] = {}
_lock = threading.Lock()


def _session_path(session_id: str) -> Path:
    safe_id = "".join(c if c.isalnum() or c in "-_." else "_" for c in session_id)
    return SESSIONS_DIR / f"{safe_id}.json"


def _persist(session_id: str, history: list[dict]) -> None:
    path = _session_path(session_id)
    path.write_text(json.dumps(history, ensure_ascii=False, indent=2), encoding="utf-8")


def restore(session_id: str) -> int:
    """Loads session history from disk into the in-memory cache.

    Does nothing if the session is already in cache or the file does not exist.
    Returns the number of messages loaded (0 if nothing was restored).
    """
    with _lock:
        if session_id in _cache:
            return 0

        path = _session_path(session_id)
        if not path.exists():
            return 0

        try:
            history = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            return 0

        _cache[session_id] = history
        log.session_restore(session_id, len(history))
        return len(history)


def append(session_id: str, role: str, content: str) -> None:
    """Appends a message to the in-memory session and persists it to disk."""
    with _lock:
        if session_id not in _cache:
            _cache[session_id] = []
            log.session_restore(session_id, 0)
        _cache[session_id].append({"role": role, "content": content})
        log.session_append(session_id, role)
        _persist(session_id, _cache[session_id])


def get_history(session_id: str) -> list[dict]:
    """Returns a snapshot of the current in-memory session history."""
    with _lock:
        return list(_cache.get(session_id, []))
