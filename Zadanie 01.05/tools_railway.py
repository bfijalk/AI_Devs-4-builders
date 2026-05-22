import json
import logging
import os
import time
import requests
from dotenv import load_dotenv

# Wspólny stan rate-limit dla wszystkich wywołań
_rate_limit_reset_at: float = 0.0

logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)

load_dotenv()

API_KEY = os.getenv("API_KEY")
BASE_URL = "https://hub.ag3nts.org/verify"


def _log_rate_limit_headers(headers: dict) -> None:
    """Odczytuje nagłówki rate-limit i aktualizuje globalny czas resetu."""
    global _rate_limit_reset_at

    interesting = {
        k: v for k, v in headers.items()
        if any(s in k.lower() for s in ("ratelimit", "rate-limit", "retry", "x-ratelimit"))
    }
    if interesting:
        logging.debug("Rate-limit headers: %s", json.dumps(dict(interesting), indent=2))

    # X-RateLimit-Reset lub RateLimit-Reset — epoch timestamp lub liczba sekund
    reset_raw = (
        headers.get("X-RateLimit-Reset")
        or headers.get("RateLimit-Reset")
        or headers.get("x-ratelimit-reset")
    )
    if reset_raw:
        try:
            val = float(reset_raw)
            # jeśli wartość wygląda jak epoch (>1e9), traktujemy jako timestamp
            _rate_limit_reset_at = val if val > 1_000_000_000 else time.time() + val
            logging.debug("Rate-limit reset zaplanowany na: %.1f (za %.1f s)", _rate_limit_reset_at, _rate_limit_reset_at - time.time())
        except ValueError:
            pass


def _wait_for_rate_limit_reset() -> None:
    """Czeka jeśli minął czas resetu rate-limitu."""
    global _rate_limit_reset_at
    now = time.time()
    if _rate_limit_reset_at > now:
        wait = _rate_limit_reset_at - now
        logging.warning("Rate-limit aktywny — czekam %.1f s do resetu...", wait)
        time.sleep(wait)
        _rate_limit_reset_at = 0.0


def _verify(answer: dict) -> dict:
    payload = {
        "apikey": API_KEY,
        "task": "railway",
        "answer": answer,
    }
    while True:
        _wait_for_rate_limit_reset()

        logging.debug("→ POST %s\n%s", BASE_URL, json.dumps(answer, ensure_ascii=False, indent=2))
        response = requests.post(BASE_URL, json=payload)
        _log_rate_limit_headers(dict(response.headers))

        if response.status_code == 503:
            try:
                body = response.json()
            except Exception:
                body = {}
            wait_sec = (
                body.get("retry_after")
                or body.get("retryAfter")
                or body.get("wait")
                or int(response.headers.get("Retry-After", 5))
            )
            logging.warning(
                "← 503 — serwer przeciążony. Czekam %s s przed ponowieniem...\n%s",
                wait_sec,
                json.dumps(body, ensure_ascii=False, indent=2),
            )
            time.sleep(int(wait_sec))
            continue

        if response.status_code == 429:
            try:
                body = response.json()
            except Exception:
                body = {}
            wait_sec = int(
                response.headers.get("Retry-After")
                or response.headers.get("X-RateLimit-Reset")
                or body.get("retry_after")
                or 10
            )
            logging.warning(
                "← 429 — przekroczono limit zapytań. Czekam %s s...\n%s",
                wait_sec,
                json.dumps(body, ensure_ascii=False, indent=2),
            )
            time.sleep(wait_sec)
            continue

        response.raise_for_status()
        result = response.json()
        logging.debug("← %d\n%s", response.status_code, json.dumps(result, ensure_ascii=False, indent=2))
        return result


def handle_help(_args: dict) -> dict:
    return _verify({"action": "help"})


def handle_getstatus(args: dict) -> dict:
    return _verify({"action": "getstatus", "route": args["route"]})


def handle_reconfigure(args: dict) -> dict:
    return _verify({"action": "reconfigure", "route": args["route"]})


def handle_setstatus(args: dict) -> dict:
    return _verify({"action": "setstatus", "route": args["route"], "value": args["value"]})


def handle_save(args: dict) -> dict:
    return _verify({"action": "save", "route": args["route"]})


_activation_state: dict = {}


def _is_error(result: dict) -> bool:
    return "error" in result or result.get("code", 0) < 0


def handle_activate_route(args: dict) -> dict:
    """Aktywuje trasę sekwencją: getstatus → reconfigure → setstatus(RTOPEN) → save.
    Zapamiętuje postęp per trasa — przy ponownym wywołaniu wznawia od nieudanego kroku."""
    route = args["route"]

    state = _activation_state.get(route, {"phase": "getstatus", "steps": []})
    phase = state["phase"]
    steps = state["steps"]

    logging.info("Trasa %s — wznawianie od fazy: %s", route, phase)

    if phase == "getstatus":
        result = _verify({"action": "getstatus", "route": route})
        steps.append({"step": "getstatus", "result": result})
        if _is_error(result):
            _activation_state[route] = {"phase": "getstatus", "steps": steps}
            return {"error": "getstatus failed", "details": result, "steps": steps}
        current = result.get("status") or result.get("value")
        if current == "RTOPEN":
            _activation_state.pop(route, None)
            logging.info("Trasa %s jest już otwarta — pomijam aktywację.", route)
            return {"ok": True, "message": f"Trasa {route} jest już aktywna (RTOPEN).", "steps": steps}
        phase = "reconfigure"

    if phase == "reconfigure":
        result = _verify({"action": "reconfigure", "route": route})
        steps.append({"step": "reconfigure", "result": result})
        if _is_error(result):
            _activation_state[route] = {"phase": "reconfigure", "steps": steps}
            return {"error": "reconfigure failed", "details": result, "steps": steps}
        phase = "setstatus"

    if phase == "setstatus":
        result = _verify({"action": "setstatus", "route": route, "value": "RTOPEN"})
        steps.append({"step": "setstatus", "result": result})
        if _is_error(result):
            _activation_state[route] = {"phase": "setstatus", "steps": steps}
            return {"error": "setstatus failed", "details": result, "steps": steps}
        phase = "save"

    if phase == "save":
        result = _verify({"action": "save", "route": route})
        steps.append({"step": "save", "result": result})
        if _is_error(result):
            _activation_state[route] = {"phase": "save", "steps": steps}
            return {"error": "save failed", "details": result, "steps": steps}

    _activation_state.pop(route, None)
    return {"ok": True, "message": f"Trasa {route} została pomyślnie aktywowana (RTOPEN).", "steps": steps}


HANDLERS = {
    "help": handle_help,
    "getstatus": handle_getstatus,
    "reconfigure": handle_reconfigure,
    "setstatus": handle_setstatus,
    "save": handle_save,
    "activate_route": handle_activate_route,
}

DEFINITIONS = [
    {
        "type": "function",
        "function": {
            "name": "help",
            "description": "Zwraca listę dostępnych akcji i parametrów API railway.",
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "getstatus",
            "description": "Pobiera aktualny status danej trasy.",
            "parameters": {
                "type": "object",
                "properties": {
                    "route": {
                        "type": "string",
                        "description": "Identyfikator trasy, np. a-1, b-12. Format: [a-z]-[0-9]{1,2}",
                    }
                },
                "required": ["route"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "reconfigure",
            "description": "Włącza tryb rekonfiguracji dla danej trasy (wymagany przed zmianą statusu).",
            "parameters": {
                "type": "object",
                "properties": {
                    "route": {
                        "type": "string",
                        "description": "Identyfikator trasy, np. a-1",
                    }
                },
                "required": ["route"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "setstatus",
            "description": "Ustawia status trasy (RTOPEN = otwarta, RTCLOSE = zamknięta). Trasa musi być wcześniej w trybie rekonfiguracji.",
            "parameters": {
                "type": "object",
                "properties": {
                    "route": {
                        "type": "string",
                        "description": "Identyfikator trasy, np. a-1",
                    },
                    "value": {
                        "type": "string",
                        "enum": ["RTOPEN", "RTCLOSE"],
                        "description": "Nowy status trasy.",
                    },
                },
                "required": ["route", "value"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "save",
            "description": "Zapisuje zmiany i wychodzi z trybu rekonfiguracji dla danej trasy.",
            "parameters": {
                "type": "object",
                "properties": {
                    "route": {
                        "type": "string",
                        "description": "Identyfikator trasy, np. a-1",
                    }
                },
                "required": ["route"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "activate_route",
            "description": (
                "Automatycznie aktywuje trasę wykonując pełną sekwencję: "
                "getstatus → reconfigure → setstatus(RTOPEN) → save. "
                "Użyj gdy użytkownik chce aktywować lub otworzyć trasę."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "route": {
                        "type": "string",
                        "description": "Identyfikator trasy, np. a-1, b-12. Format: [a-z]-[0-9]{1,2}",
                    }
                },
                "required": ["route"],
            },
        },
    },
]
