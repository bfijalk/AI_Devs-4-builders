import sys

_R = "\x1b[0m"
_BOLD = "\x1b[1m"
_DIM = "\x1b[2m"
_GREEN = "\x1b[32m"
_RED = "\x1b[31m"
_YELLOW = "\x1b[33m"
_CYAN = "\x1b[36m"
_MAGENTA = "\x1b[35m"


def _print(label: str, color: str, *parts: str, file=sys.stdout) -> None:
    prefix = f"{color}{_BOLD}[{label}]{_R}"
    print(prefix, *parts, flush=True, file=file)


def server_start(host: str, port: int, model: str) -> None:
    print(f"\n{_BOLD}{'═' * 56}{_R}")
    print(f"{_GREEN}{_BOLD}  Serwer nasłuchuje na http://{host}:{port}/{_R}")
    print(f"{_DIM}  Model: {model}{_R}")
    print(f"{_BOLD}{'═' * 56}{_R}\n")


def request(session_id: str, message: str) -> None:
    print(f"\n{_BOLD}{'─' * 56}{_R}")
    _print("REQUEST", _CYAN, f"session={_BOLD}{session_id}{_R}  msg={_DIM}{message!r}{_R}")


def session_restore(session_id: str, count: int) -> None:
    if count > 0:
        _print("SESSION", _YELLOW, f"Restored {count} messages for '{session_id}'")
    else:
        _print("SESSION", _YELLOW, f"New session '{session_id}'")


def session_append(session_id: str, role: str) -> None:
    icon = "→" if role == "user" else "←"
    _print("SESSION", _YELLOW, f"{icon} append role={_BOLD}{role}{_R}  session='{session_id}'")


def tool_call(name: str, args: dict) -> None:
    _print("TOOL", _YELLOW, f"→ {_BOLD}{name}{_R}({_DIM}{args}{_R})")


def tool_result(name: str, result) -> None:
    _print("TOOL", _YELLOW, f"← {_BOLD}{name}{_R} = {_DIM}{result!r}{_R}")


def ai_call(message_count: int) -> None:
    _print("AI", _MAGENTA, f"Sending {message_count} messages to LLM…")


def ai_response(reply: str) -> None:
    _print("AI", _MAGENTA, f"Reply: {_DIM}{reply!r}{_R}")


def response(session_id: str, reply: str) -> None:
    _print("RESPONSE", _GREEN, f"session={_BOLD}{session_id}{_R}  msg={_DIM}{reply!r}{_R}")


def error(context: str, exc: Exception) -> None:
    _print("ERROR", _RED, f"{context}: {exc}", file=sys.stderr)
