"""
Integration tests for the chat server.

Usage:
    venv/bin/python test.py
    venv/bin/python test.py --url http://localhost:3000
"""

import sys
import json
import argparse
import urllib.request
import urllib.error
from typing import Optional

BASE_URL = "http://localhost:3000"

_PASS = "\x1b[32m✓\x1b[0m"
_FAIL = "\x1b[31m✗\x1b[0m"
_BOLD = "\x1b[1m"
_DIM = "\x1b[2m"
_R = "\x1b[0m"

_passed = 0
_failed = 0


def post(session_id: Optional[str], msg: Optional[str]) -> tuple:
    payload = {}
    if session_id is not None:
        payload["sessionID"] = session_id
    if msg is not None:
        payload["msg"] = msg

    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        BASE_URL,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())


def check(description: str, condition: bool, detail: str = "") -> None:
    global _passed, _failed
    icon = _PASS if condition else _FAIL
    suffix = f"  {_DIM}{detail}{_R}" if detail else ""
    print(f"  {icon} {description}{suffix}")
    if condition:
        _passed += 1
    else:
        _failed += 1


def section(title: str) -> None:
    print(f"\n{_BOLD}{title}{_R}")


def run_tests() -> None:
    section("1. Walidacja wejścia")

    status, body = post(None, "hello")
    check("Brak sessionID → 400", status == 400, f"status={status}")

    status, body = post("s1", None)
    check("Brak msg → 400", status == 400, f"status={status}")

    status, body = post(None, None)
    check("Brak obu pól → 400", status == 400, f"status={status}")

    section("2. Podstawowa odpowiedź")

    status, body = post("basic-test", "Odpowiedz tylko słowem: tak")
    check("Status 200", status == 200, f"status={status}")
    check("Pole 'msg' w odpowiedzi", "msg" in body, f"keys={list(body)}")
    check("Odpowiedź niepusta", bool(body.get("msg")), f"msg={body.get('msg')!r}")

    section("3. Pamięć w ramach sesji")

    sid = "memory-test"
    post(sid, "Moje imię to Zofia. Zapamiętaj.")
    status, body = post(sid, "Jak mam na imię?")
    reply = body.get("msg", "")
    check("Status 200", status == 200)
    check("Odpowiedź zawiera imię 'Zofia'", "Zofia" in reply, f"reply={reply!r}")

    section("4. Izolacja sesji")

    post("isolation-A", "Moje ulubione miasto to Kraków.")
    post("isolation-B", "Moje ulubione miasto to Gdańsk.")

    _, body_a = post("isolation-A", "Jakie jest moje ulubione miasto?")
    _, body_b = post("isolation-B", "Jakie jest moje ulubione miasto?")

    reply_a = body_a.get("msg", "")
    reply_b = body_b.get("msg", "")

    check("Sesja A pamięta Kraków", "Kraków" in reply_a, f"reply={reply_a!r}")
    check("Sesja B pamięta Gdańsk", "Gdańsk" in reply_b, f"reply={reply_b!r}")
    check("Sesja A nie mówi o Gdańsku", "Gdańsk" not in reply_a, f"reply={reply_a!r}")
    check("Sesja B nie mówi o Krakowie", "Kraków" not in reply_b, f"reply={reply_b!r}")

    section("5. Wiele wiadomości w sesji (wątek konwersacji)")

    sid = "thread-test"
    post(sid, "Liczba A to 7.")
    post(sid, "Liczba B to 13.")
    _, body = post(sid, "Ile wynosi A + B?")
    reply = body.get("msg", "")
    check("Odpowiedź zawiera wynik 20", "20" in reply, f"reply={reply!r}")


def main() -> None:
    global BASE_URL

    parser = argparse.ArgumentParser()
    parser.add_argument("--url", default=BASE_URL, help="Base URL serwera")
    args = parser.parse_args()
    BASE_URL = args.url.rstrip("/")

    print(f"{_BOLD}Testy integracyjne → {BASE_URL}{_R}")

    try:
        run_tests()
    except OSError as e:
        print(f"\n\x1b[31mNie można połączyć się z serwerem ({e})\x1b[0m")
        print("Uruchom najpierw: venv/bin/python server.py")
        sys.exit(1)

    total = _passed + _failed
    color = "\x1b[32m" if _failed == 0 else "\x1b[31m"
    print(f"\n{_BOLD}Wynik: {color}{_passed}/{total}{_R}{_BOLD} testów zaliczonych{_R}\n")
    sys.exit(0 if _failed == 0 else 1)


if __name__ == "__main__":
    main()
