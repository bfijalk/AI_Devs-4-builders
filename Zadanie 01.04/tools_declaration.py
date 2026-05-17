"""
Narzędzia agenta deklaracji (punkt 2):

read_file(filename)         — wczytuje plik z folderu files/
find_route(origin, dest)    — wyznacza trasę na podstawie grafu połączeń z index.md
calculate_fee(...)          — oblicza opłatę wg regulaminu SPK
fill_declaration(data)      — wypełnia wzór z zalacznik-E.md i zapisuje deklaracja.md
send_to_verify(text)        — wysyła deklarację na https://hub.ag3nts.org/verify
"""

import json
import math
import os
import re
import urllib.request
import urllib.error
from collections import deque
from datetime import date
from pathlib import Path
from typing import Optional

from dotenv import load_dotenv

import log

load_dotenv()

FILES_DIR = Path(__file__).parent / "files"
VERIFY_URL = "https://hub.ag3nts.org/verify"
API_KEY = os.getenv("API_KEY", "")


def _region_crossings(cities: list[str], regions: dict[str, str]) -> int:
    """Liczy ile granic regionów przekracza trasa przez podane miasta."""
    city_regions = [regions.get(c, "Nieznany") for c in cities]
    crossings = 0
    for i in range(1, len(city_regions)):
        if city_regions[i] != city_regions[i - 1]:
            crossings += 1
    return crossings


# ── Dynamiczny parser tras ────────────────────────────────────────────────────

# Wiersz tabeli tras: | KOD | Miasto A - Miasto B | 123 | ... |
_ROUTE_ROW_RE = re.compile(
    r"^\|\s*([A-Z]+-\d+)\s*\|"          # kod trasy
    r"\s*([^|]+?)\s*-\s*([^|]+?)\s*\|"  # miasto A - miasto B
    r"\s*(\d+)\s*\|",                    # długość km
    re.MULTILINE,
)

# Wiersz tabeli tras wyłączonych: | X-01 | Gdańsk – Żarnowiec | ... |
_EXCLUDED_ROW_RE = re.compile(
    r"^\|\s*(X-\d+)\s*\|"
    r"\s*([^|]+?)\s*[–-]\s*([^|]+?)\s*\|",
    re.MULTILINE,
)

# Szacowane długości tras wyłączonych (km) wg sekcji 3.2 / trasy-wylaczone.md
# Trasy X nie mają podanych km w dokumentacji — przyjmujemy ~80 km dla X-01 (Gdańsk–Żarnowiec, ~80 km wg mapy)
_EXCLUDED_KM_FALLBACK = 80

_graph_cache: Optional[dict[str, list[tuple[str, str, int]]]] = None
_regions_cache: Optional[dict[str, str]] = None


def _parse_routes_from_docs() -> tuple[
    dict[str, list[tuple[str, str, int]]],
    dict[str, str],
]:
    """
    Parsuje trasy z files/index.md i files/trasy-wylaczone.md.
    Zwraca (graph, regions).
    """
    routes: list[tuple[str, str, str, int]] = []
    regions: dict[str, str] = {}

    index_path = FILES_DIR / "index.md"
    if not index_path.exists():
        log.error("_parse_routes_from_docs", FileNotFoundError("files/index.md nie istnieje"))
        return {}, {}

    text = index_path.read_text(encoding="utf-8", errors="replace")

    # Parsuj trasy aktywne z tabel M/R/L
    for m in _ROUTE_ROW_RE.finditer(text):
        code = m.group(1).strip()
        city_a = m.group(2).strip()
        city_b = m.group(3).strip()
        try:
            km = int(m.group(4).strip())
        except ValueError:
            continue
        routes.append((code, city_a, city_b, km))

        # Ustal region na podstawie kodu trasy (M=magistralna/Centralny, R=regionalny)
        prefix = code[0]
        if prefix == "M":
            for city in (city_a, city_b):
                if city not in regions:
                    regions[city] = _infer_region(city)
        else:
            for city in (city_a, city_b):
                if city not in regions:
                    regions[city] = _infer_region(city)

    # Parsuj trasy wyłączone z trasy-wylaczone.md
    excluded_path = FILES_DIR / "trasy-wylaczone.md"
    if excluded_path.exists():
        exc_text = excluded_path.read_text(encoding="utf-8", errors="replace")
        for m in _EXCLUDED_ROW_RE.finditer(exc_text):
            code = m.group(1).strip()
            city_a = m.group(2).strip()
            city_b = m.group(3).strip()
            routes.append((code, city_a, city_b, _EXCLUDED_KM_FALLBACK))
            for city in (city_a, city_b):
                if city not in regions:
                    regions[city] = _infer_region(city)

    # Zbuduj graf
    graph: dict[str, list[tuple[str, str, int]]] = {}
    for code, a, b, km in routes:
        graph.setdefault(a, []).append((code, b, km))
        graph.setdefault(b, []).append((code, a, km))

    log.info(
        f"Graf tras: {len(routes)} tras, {len(graph)} miast "
        f"(z tego {sum(1 for c,_,_,_ in routes if c.startswith('X'))} wyłączonych)"
    )
    return graph, regions


def _infer_region(city: str) -> str:
    """Heurystyczne przypisanie regionu na podstawie nazwy miasta."""
    north = {"Gdańsk", "Szczecin", "Bydgoszcz", "Toruń", "Elbląg", "Olsztyn",
             "Żarnowiec", "Tczew", "Tczew", "Stargard", "Ełk", "Łomża", "Wejherowo",
             "Lębork", "Krokowa"}
    central = {"Warszawa", "Łódź", "Poznań", "Białystok", "Lublin", "Radom",
               "Gniezno", "Chełm"}
    south = {"Kraków", "Katowice", "Wrocław", "Częstochowa", "Kielce", "Rzeszów",
             "Tarnów", "Bielsko-Biała", "Legnica", "Przemyśl", "Nowy Sącz"}
    west = {"Zielona Góra", "Stargard", "Legnica", "Jawor"}

    if city in north:
        return "Północny"
    if city in central:
        return "Centralny"
    if city in south:
        return "Południowy"
    if city in west:
        return "Zachodni"
    return "Nieznany"


def _get_graph() -> tuple[dict[str, list[tuple[str, str, int]]], dict[str, str]]:
    """Zwraca (graph, regions), parsując pliki przy pierwszym wywołaniu (lazy init)."""
    global _graph_cache, _regions_cache
    if _graph_cache is None:
        _graph_cache, _regions_cache = _parse_routes_from_docs()
    return _graph_cache, _regions_cache or {}


# ── Narzędzia ─────────────────────────────────────────────────────────────────

def read_file(filename: str) -> dict:
    """Wczytuje plik z folderu files/ i zwraca jego zawartość."""
    log.tool_call("read_file", {"filename": filename})
    path = FILES_DIR / filename
    if not path.exists():
        result = {"error": f"Plik nie istnieje: {filename}"}
        log.tool_result("read_file", result)
        return result
    content = path.read_text(encoding="utf-8", errors="replace")
    result = {"filename": filename, "content": content, "chars": len(content)}
    log.tool_result("read_file", {"filename": filename, "chars": len(content)})
    return result


def find_route(origin: str, destination: str) -> dict:
    """
    Wyznacza trasę między dwoma miastami w sieci SPK.
    Graf budowany dynamicznie z files/index.md i files/trasy-wylaczone.md.
    Zwraca kod/kody tras, łączną odległość i liczbę przekraczanych granic regionów.
    """
    log.tool_call("find_route", {"origin": origin, "destination": destination})

    graph, regions = _get_graph()

    if origin not in graph:
        result = {"error": f"Miasto '{origin}' nie istnieje w sieci SPK. Dostępne: {sorted(graph)[:20]}"}
        log.tool_result("find_route", result)
        return result
    if destination not in graph:
        result = {"error": f"Miasto '{destination}' nie istnieje w sieci SPK. Dostępne: {sorted(graph)[:20]}"}
        log.tool_result("find_route", result)
        return result

    if origin == destination:
        result = {
            "route_codes": [],
            "route_description": f"{origin} (bez przejazdu)",
            "total_km": 0,
            "region_crossings": 0,
        }
        log.tool_result("find_route", result)
        return result

    # BFS — minimalizujemy liczbę przesiadek, tie-break: najmniejsza suma km
    # Stan: (miasto, [kody_tras], [miasta_na_trasie], suma_km)
    queue: deque = deque()
    queue.append((origin, [], [origin], 0))
    visited: dict[str, int] = {origin: 0}  # miasto → najniższe km

    best: Optional[tuple] = None

    while queue:
        city, codes, path_cities, total_km = queue.popleft()

        if city == destination:
            if best is None or len(codes) < len(best[0]) or (
                len(codes) == len(best[0]) and total_km < best[2]
            ):
                best = (codes, path_cities, total_km)
            continue

        for code, neighbor, km in graph.get(city, []):
            new_km = total_km + km
            if neighbor not in visited or visited[neighbor] > new_km:
                visited[neighbor] = new_km
                queue.append((neighbor, codes + [code], path_cities + [neighbor], new_km))

    if best is None:
        result = {"error": f"Brak trasy między '{origin}' a '{destination}'."}
        log.tool_result("find_route", result)
        return result

    codes, path_cities, total_km = best
    crossings = _region_crossings(path_cities, regions)
    description = " → ".join(
        f"{path_cities[i]} ({codes[i]})" for i in range(len(codes))
    ) + f" → {destination}"

    result = {
        "route_codes": codes,
        "route_code": " + ".join(codes),
        "route_description": description,
        "cities": path_cities,
        "total_km": total_km,
        "region_crossings": crossings,
    }
    log.tool_result("find_route", result)
    return result


def calculate_fee(
    category: str,
    weight_kg: float,
    distance_km: int,
    region_crossings: int,
) -> dict:
    """
    Oblicza opłatę za przesyłkę zgodnie z regulaminem SPK.
    Kategorie A i B są zwolnione z opłat (0 PP łącznie).
    """
    log.tool_call("calculate_fee", {
        "category": category, "weight_kg": weight_kg,
        "distance_km": distance_km, "region_crossings": region_crossings,
    })

    category = category.upper()

    # Kat. A i B — całkowite zwolnienie (w tym opłata za wagony dodatkowe)
    if category in ("A", "B"):
        wdp = max(0, math.ceil(weight_kg / 500) - 2)  # standardowy skład = 2 wagony × 500 kg
        result = {"OB": 0, "OW": 0, "OT": 0, "total": 0, "currency": "PP",
                  "wdp": wdp,
                  "note": (
                      f"Kategoria {category} — zwolniona z opłat (finansowana przez System). "
                      f"WDP = {wdp} (standardowy skład 2×500 kg = 1000 kg, "
                      f"przesyłka {weight_kg} kg wymaga {wdp} dodatkowych wagonów)."
                  )}
        log.tool_result("calculate_fee", result)
        return result

    # Opłata bazowa
    base_fees = {"C": 2, "D": 5, "E": 10}
    if category not in base_fees:
        result = {"error": f"Nieznana kategoria: {category}"}
        log.tool_result("calculate_fee", result)
        return result
    OB = base_fees[category]

    # Opłata wagowa
    w = weight_kg
    OW = 0.0
    brackets = [
        (5.0,    0.5),
        (25.0,   1.0),
        (100.0,  2.0),
        (500.0,  3.0),
        (1000.0, 5.0),
    ]
    remaining = w
    prev = 0.0
    for limit, rate in brackets:
        if remaining <= 0:
            break
        chunk = min(remaining, limit - prev)
        OW += chunk * rate
        remaining -= chunk
        prev = limit
    if remaining > 0:
        OW += remaining * 7.0  # 1000+ kg

    # Opłata trasowa (za każde rozpoczęte 100 km)
    hundreds = math.ceil(distance_km / 100)
    if region_crossings == 0:
        rate_per_100 = 1
    elif region_crossings == 1:
        rate_per_100 = 2
    else:
        rate_per_100 = 3
    OT = hundreds * rate_per_100

    total = round(OB + OW + OT, 2)

    result = {
        "OB": OB,
        "OW": round(OW, 2),
        "OT": OT,
        "total": total,
        "currency": "PP",
    }
    log.tool_result("calculate_fee", result)
    return result


def fill_declaration(data: dict) -> dict:
    """
    Wypełnia wzór deklaracji z zalacznik-E.md danymi przesyłki.
    Zachowuje oryginalny format (ramki === i ---).
    Zapisuje wynik do files/deklaracja.md i zwraca tekst deklaracji.

    Oczekiwane klucze w data:
      date, origin, sender_id, destination, route_code,
      category, description, weight_kg, wdp, notes, fee
    """
    log.tool_call("fill_declaration", data)

    declaration = (
        "SYSTEM PRZESYŁEK KONDUKTORSKICH - DEKLARACJA ZAWARTOŚCI\n"
        "======================================================\n"
        f"DATA: {data.get('date', date.today().isoformat())}\n"
        f"PUNKT NADAWCZY: {data.get('origin', '')}\n"
        "------------------------------------------------------\n"
        f"NADAWCA: {data.get('sender_id', '')}\n"
        f"PUNKT DOCELOWY: {data.get('destination', '')}\n"
        f"TRASA: {data.get('route_code', '')}\n"
        "------------------------------------------------------\n"
        f"KATEGORIA PRZESYŁKI: {data.get('category', '')}\n"
        "------------------------------------------------------\n"
        f"OPIS ZAWARTOŚCI (max 200 znaków): {data.get('description', '')}\n"
        "------------------------------------------------------\n"
        f"DEKLAROWANA MASA (kg): {data.get('weight_kg', '')}\n"
        "------------------------------------------------------\n"
        f"WDP: {data.get('wdp', 0)}\n"
        "------------------------------------------------------\n"
        f"UWAGI SPECJALNE: {data.get('notes', 'Brak')}\n"
        "------------------------------------------------------\n"
        f"KWOTA DO ZAPŁATY: {data.get('fee', '0')} PP\n"
        "------------------------------------------------------\n"
        "OŚWIADCZAM, ŻE PODANE INFORMACJE SĄ PRAWDZIWE.\n"
        "BIORĘ NA SIEBIE KONSEKWENCJĘ ZA FAŁSZYWE OŚWIADCZENIE.\n"
        "======================================================"
    )

    out_path = FILES_DIR / "deklaracja.md"
    out_path.write_text(declaration, encoding="utf-8")
    log.save(str(out_path))

    result = {"declaration_text": declaration, "saved_to": str(out_path), "chars": len(declaration)}
    log.tool_result("fill_declaration", {"saved_to": str(out_path), "chars": len(declaration)})
    return result


def send_to_verify(declaration_text: str) -> dict:
    """Wysyła wypełnioną deklarację na endpoint /verify."""
    log.tool_call("send_to_verify", {"chars": len(declaration_text)})

    payload = json.dumps({
        "apikey": API_KEY,
        "task": "sendit",
        "answer": {"declaration": declaration_text},
    }).encode("utf-8")

    req = urllib.request.Request(
        VERIFY_URL,
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            body = resp.read().decode("utf-8")
        try:
            result = json.loads(body)
        except Exception:
            result = {"raw": body}
        log.tool_result("send_to_verify", result)
        return result
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        result = {"error": f"HTTP {e.code}", "body": body}
        log.tool_result("send_to_verify", result)
        return result
    except Exception as exc:
        result = {"error": str(exc)}
        log.tool_result("send_to_verify", result)
        return result


# ── LLM tool definitions & dispatch ───────────────────────────────────────────

DEFINITIONS = [
    {
        "type": "function",
        "function": {
            "name": "read_file",
            "description": (
                "Wczytuje plik z folderu files/ i zwraca jego zawartość tekstową. "
                "Użyj aby przejrzeć dokumentację, wzory deklaracji lub inne pliki."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "filename": {
                        "type": "string",
                        "description": "Nazwa pliku w folderze files/, np. index.md, zalacznik-E.md",
                    },
                },
                "required": ["filename"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "find_route",
            "description": (
                "Wyznacza trasę między dwoma miastami w sieci SPK. "
                "Zwraca kod trasy, całkowitą odległość i liczbę przekraczanych granic regionów."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "origin": {
                        "type": "string",
                        "description": "Miasto nadania, np. Warszawa",
                    },
                    "destination": {
                        "type": "string",
                        "description": "Miasto docelowe, np. Kraków",
                    },
                },
                "required": ["origin", "destination"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "calculate_fee",
            "description": (
                "Oblicza opłatę za przesyłkę wg regulaminu SPK. "
                "Kategorie A i B są zwolnione z opłat (całkowity koszt = 0 PP)."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "category": {
                        "type": "string",
                        "description": "Kategoria przesyłki: A, B, C, D lub E",
                    },
                    "weight_kg": {
                        "type": "number",
                        "description": "Masa przesyłki w kilogramach",
                    },
                    "distance_km": {
                        "type": "integer",
                        "description": "Łączna długość trasy w kilometrach",
                    },
                    "region_crossings": {
                        "type": "integer",
                        "description": "Liczba przekraczanych granic regionów (0, 1 lub 2+)",
                    },
                },
                "required": ["category", "weight_kg", "distance_km", "region_crossings"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "fill_declaration",
            "description": (
                "Wypełnia wzór deklaracji SPK danymi przesyłki. "
                "Zachowuje oryginalny format z zalacznik-E.md. "
                "Zapisuje wynik do files/deklaracja.md i zwraca tekst deklaracji."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "date": {"type": "string", "description": "Data w formacie YYYY-MM-DD"},
                    "origin": {"type": "string", "description": "Miasto nadania"},
                    "sender_id": {"type": "string", "description": "Identyfikator nadawcy"},
                    "destination": {"type": "string", "description": "Miasto docelowe"},
                    "route_code": {"type": "string", "description": "Kod trasy, np. M-02"},
                    "category": {"type": "string", "description": "Kategoria przesyłki (A-E)"},
                    "description": {"type": "string", "description": "Opis zawartości (max 200 znaków)"},
                    "weight_kg": {"type": "number", "description": "Masa w kg"},
                    "wdp": {"type": "integer", "description": "Liczba wagonów dodatkowych płatnych"},
                    "notes": {"type": "string", "description": "Uwagi specjalne"},
                    "fee": {"type": "string", "description": "Kwota do zapłaty, np. 0"},
                },
                "required": [
                    "date", "origin", "sender_id", "destination", "route_code",
                    "category", "description", "weight_kg", "wdp", "notes", "fee",
                ],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "send_to_verify",
            "description": (
                "Wysyła wypełnioną deklarację na endpoint /verify systemu SPK. "
                "Użyj jako ostatni krok po wypełnieniu deklaracji."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "declaration_text": {
                        "type": "string",
                        "description": "Pełny tekst wypełnionej deklaracji",
                    },
                },
                "required": ["declaration_text"],
            },
        },
    },
]

def _send_to_verify_handler(args: dict) -> dict:
    text = args.get("declaration_text", "")
    # Jeśli LLM nie przekazał tekstu — wczytaj ostatnio zapisaną deklarację z dysku
    if not text or len(text) < 50:
        saved = FILES_DIR / "deklaracja.md"
        if saved.exists():
            log.info("send_to_verify: brak tekstu w argumencie, wczytuję files/deklaracja.md")
            text = saved.read_text(encoding="utf-8")
        else:
            return {"error": "Brak tekstu deklaracji i plik deklaracja.md nie istnieje."}
    return send_to_verify(text)


HANDLERS = {
    "read_file":        lambda args: read_file(**args),
    "find_route":       lambda args: find_route(**args),
    "calculate_fee":    lambda args: calculate_fee(**args),
    "fill_declaration": lambda args: fill_declaration(args),
    "send_to_verify":   _send_to_verify_handler,
}
