import sys

_R = "\x1b[0m"
_BOLD = "\x1b[1m"
_DIM = "\x1b[2m"
_GREEN = "\x1b[32m"
_RED = "\x1b[31m"
_YELLOW = "\x1b[33m"
_CYAN = "\x1b[36m"
_MAGENTA = "\x1b[35m"
_BLUE = "\x1b[34m"


def _print(label: str, color: str, *parts: str, file=sys.stdout) -> None:
    prefix = f"{color}{_BOLD}[{label}]{_R}"
    print(prefix, *parts, flush=True, file=file)


def info(message: str) -> None:
    _print("INFO", _CYAN, message)


def fetch_start(url: str) -> None:
    _print("FETCH", _BLUE, f"→ {_DIM}{url}{_R}")


def fetch_ok(url: str, size: int) -> None:
    _print("FETCH", _GREEN, f"✓ {_DIM}{url}{_R}  ({size} bytes)")


def fetch_error(url: str, exc: Exception) -> None:
    _print("FETCH", _RED, f"✗ {_DIM}{url}{_R}  {exc}", file=sys.stderr)


def save(path: str) -> None:
    _print("SAVE", _GREEN, f"→ {_DIM}{path}{_R}")


def refs_found(count: int, parent: str) -> None:
    _print("REFS", _YELLOW, f"{count} referencji znaleziono w {_DIM}{parent}{_R}")


def tool_call(name: str, args: dict) -> None:
    _print("TOOL", _YELLOW, f"→ {_BOLD}{name}{_R}({_DIM}{args}{_R})")


def tool_result(name: str, result: object) -> None:
    preview = repr(result)[:200]
    _print("TOOL", _YELLOW, f"← {_BOLD}{name}{_R} = {_DIM}{preview}{_R}")


def ai_call(message_count: int) -> None:
    _print("AI", _MAGENTA, f"Wysyłam {message_count} wiadomości do LLM…")


def ai_response(reply: str) -> None:
    _print("AI", _MAGENTA, f"Odpowiedź: {_DIM}{reply!r}{_R}")


def error(context: str, exc: Exception) -> None:
    _print("ERROR", _RED, f"{context}: {exc}", file=sys.stderr)
