import json
import os
import re
from datetime import datetime
from typing import Optional

import tiktoken
import requests
from openai import OpenAI
from dotenv import load_dotenv

import log

load_dotenv()

API_KEY = os.getenv("API_KEY")
VERIFY_URL = "https://hub.ag3nts.org/verify"
LOG_URL = f"https://hub.ag3nts.org/data/{API_KEY}/failure.log"

_client = OpenAI(
    api_key=os.getenv("OPEN_ROUTER_API_KEY"),
    base_url=os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1"),
)
MODEL = os.getenv("OPEN_ROUTER_MODEL", "gpt-4o")

FILES_DIR = "Files"
LOG_PATH = os.path.join(FILES_DIR, "failure.log")
FILTERED_LOG_PATH = os.path.join(FILES_DIR, "filtered_log.log")
PREAGGREG_LOG_PATH = os.path.join(FILES_DIR, "preaggregated_log.log")
AGGREGATED_LOG_PATH = os.path.join(FILES_DIR, "aggregated_log.log")
FINAL_COMPRESSED_PATH = os.path.join(FILES_DIR, "final_compressed_log.log")

TOKEN_LIMIT = 1500
MAX_COMPRESS_ROUNDS = 3
SEP = "-" * 60

_TS_RE = re.compile(r"\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]")
_LEVEL_RE = re.compile(r"\[(ERRO|CRIT)\]")
_MSG_RE = re.compile(r"\]\s*\[(ERRO|CRIT)\]\s*(.*)")
_COMPONENT_RE = re.compile(r"(ECCS8|WTRPMP|WTANK07|STMTURB12|PWR01|WSTPOOL2|FIRMWARE)")

PHRASE_REPLACEMENTS = [
    ("absorption path reached emergency boundary. Heat rejection is no longer sufficient",
     "emerg bound, heat rejection insufficient"),
    ("reported runaway outlet temperature. Protection interlock initiated reactor trip",
     "runaway temp, reactor trip"),
    ("core cooling cannot maintain safe gradient. Immediate protective actions are required",
     "core cool fail, immediate action required"),
    ("lost stable prime under peak thermal demand. Core loop continuity is compromised",
     "lost prime, core loop compromised"),
    ("decoupling sequence forced by thermal risk. Energy conversion is terminated",
     "decoupling forced by thermal risk"),
    ("entered emergency guard branch after repeated safety faults. Manual override is locked",
     "emerg guard, manual override locked"),
    ("validation queue returned nonblocking fault set. Runtime proceeds in constrained mode",
     "validation fault, constrained mode"),
    ("feedback loop exceeded correction budget. Thermal conversion rate is reduced",
     "feedback loop exceeded correction budget"),
    ("transient disturbed auxiliary pump control. Recovery completed with degraded margin",
     "pump ctrl disturbed, degraded margin"),
    ("suction profile is inconsistent with expected coolant volume. Mechanical stress is increasing",
     "suction incons, stress increasing"),
    ("reported repeated cavitation signatures. Output pressure cannot be held at requested level",
     "cavitation, pressure unstable"),
    ("return circuit temperature rose faster than prediction. Emergency bias remains armed",
     "return temp rise, emerg bias armed"),
    ("Cooling efficiency on ECCS8 dropped below operational target. Compensating commands did not recover nominal state",
     "ECCS8 cool eff low, recovery failed"),
    ("cooling efficiency on ECCS8 dropped below operational target. Compensating commands did not recover nominal state",
     "ECCS8 cool eff low, recovery failed"),
    ("level estimate dropped near minimum reserve line. Automatic refill request timed out",
     "near min reserve, refill timeout"),
    ("indicates unstable refill trend. Available coolant inventory is no longer guaranteed",
     "refill unstable, inventory uncertain"),
    ("coolant level is below critical threshold. Shutdown logic is moving to hard trip stage",
     "coolant below thresh, hard trip"),
    ("Heat transfer path to WSTPOOL2 is saturated. Dissipation lag continues to accumulate",
     "WSTPOOL2 heat xfer saturated"),
    ("can no longer sustain stable feed for cooling auxiliaries. Critical loads are shedding",
     "feed unstable, loads shedding"),
    ("fails to recover thermal margin while WTANK07 remains partially filled. Shutdown criteria are approaching",
     "thermal margin fail, shutdown approaching"),
    ("cannot remove heat with the current WTANK07 volume. Reactor protection initiates critical stop",
     "cannot remove heat, reactor stop"),
    ("Coolant level in WTANK07 is below critical reserve for sustained operation. Protective shutdown path is being enforced",
     "WTANK07 low reserve, shutdown enforced"),
    ("Coolant inventory in WTANK07 is below critical threshold for full-loop operation. ECCS8 cannot guarantee reactor heat removal and automatic shutdown is mandatory",
     "WTANK07 critical, ECCS8 cannot guarantee heat removal, shutdown mandatory"),
    ("entered critical protection state during startup. Immediate shutdown safeguards remain active",
     "critical prot state, shutdown safeguards active"),
    ("failed a recovery step in the active sequence. The subsystem remains in degraded operation mode",
     "recovery step failed, degraded mode"),
    ("returned inconsistent feedback under load. Automatic fallback path has been applied",
     "inconsistent feedback, fallback applied"),
    ("Operational fault persisted on ECCS8 after retry cycle. Performance constraints are now enforced",
     "ECCS8 fault persisted, constraints enforced"),
    ("Control response from WTANK07 exceeded error budget. Further recovery attempts are limited",
     "WTANK07 error budget exceeded, recovery limited"),
    ("Cross-check between FIRMWARE and hardware interface map did not complete successfully. Compatibility verification remains unresolved for startup state",
     "FIRMWARE hw cross-check failed, compat unresolved"),
    ("Final trip complete because WTANK07 remained under critical water level. FIRMWARE confirms safe shutdown state with all core operations halted",
     "Final trip, WTANK07 low water. FIRMWARE safe shutdown confirmed"),
]

AGGREGATE_SYSTEM_PROMPT = """\
Compress these reactor logs to fit UNDER 1500 tokens. Currently they have {token_count} tokens.

STRICT FORMAT: one line = one event.
[YYYY-MM-DD HH:MM] [LEVEL] SUBSYSTEM short description

Rules:
- Keep EVERY subsystem: ECCS8, WTRPMP, WTANK07, STMTURB12, PWR01, WSTPOOL2, FIRMWARE.
- Each subsystem must appear at least once in output.
- Keep [ERRO] or [CRIT] level on every line.
- Maintain chronological order.
- If same event repeats in multiple hours, keep first and last occurrence only.
- Shorten descriptions but keep subsystem name and fault type.
- Drop lines about env vars, SAFETY_CHECK, or non-plant issues.
- Output ONLY log lines. No blank lines, no commentary."""

FINAL_COMPRESS_PROMPT = """\
COMPRESS from {token_count} to UNDER {target} tokens (cut {cut}%).

Rules:
- One line = one event. Format: [YYYY-MM-DD HH:MM] [LEVEL] SUBSYSTEM description
- Keep ALL subsystems (ECCS8, WTRPMP, WTANK07, STMTURB12, PWR01, WSTPOOL2, FIRMWARE).
- Keep ALL FIRMWARE events (validation fault, emerg guard, safe shutdown).
- If same event repeats, keep first and last only.
- Strict chronological order. No blanks. ONLY log lines."""


# ---------------------------------------------------------------------------
# Helper functions
# ---------------------------------------------------------------------------

def _parse_timestamp(line: str) -> Optional[datetime]:
    m = _TS_RE.match(line)
    if m:
        return datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
    return None


def _bucket_key(dt: datetime) -> str:
    return dt.strftime("%Y-%m-%d %H:00")


def _extract_message(line: str) -> str:
    m = _MSG_RE.search(line)
    return m.group(2).strip() if m else line.strip()


def _shorten_phrases(text: str) -> str:
    for long, short in PHRASE_REPLACEMENTS:
        text = text.replace(long, short)
    return text


def _strip_punctuation(text: str) -> str:
    return text.replace(".", "").replace(",", "")


def count_tokens(text: str) -> int:
    enc = tiktoken.encoding_for_model("gpt-4o")
    return len(enc.encode(text))


def ask_llm(system_prompt: str, user_prompt: str,
            max_tokens: Optional[int] = None) -> str:
    log.ai_call(2)
    kwargs = {
        "model": MODEL,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_prompt},
        ],
    }
    if max_tokens is not None:
        kwargs["max_tokens"] = max_tokens
        log.info(f"  max_tokens = {max_tokens}")
    response = _client.chat.completions.create(**kwargs)
    reply = response.choices[0].message.content or ""
    log.ai_response(reply[:300])
    return reply


# ---------------------------------------------------------------------------
# Pipeline steps
# ---------------------------------------------------------------------------

def download_log() -> str:
    os.makedirs(FILES_DIR, exist_ok=True)
    log.fetch_start(LOG_URL)
    response = requests.get(LOG_URL)
    response.raise_for_status()
    with open(LOG_PATH, "w", encoding="utf-8") as f:
        f.write(response.text)
    log.fetch_ok(LOG_URL, len(response.content))
    log.save(LOG_PATH)
    return LOG_PATH


def analyze_log(filepath: str) -> dict:
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()
    lines = content.splitlines()
    token_count = count_tokens(content)
    file_size = os.path.getsize(filepath)
    log.analyze(f"Rozmiar: {file_size:,} B | Linie: {len(lines):,} | Tokeny: {token_count:,}")
    return {
        "content": content, "lines": lines,
        "line_count": len(lines), "token_count": token_count, "file_size": file_size,
    }


def filter_log(input_path: str, output_path: str = FILTERED_LOG_PATH) -> str:
    """Zachowuje tylko linie [ERRO] i [CRIT]."""
    pattern = re.compile(r"\[(ERRO|CRIT)\]")
    kept = total = 0
    with open(input_path, "r", encoding="utf-8") as fin, \
         open(output_path, "w", encoding="utf-8") as fout:
        for line in fin:
            total += 1
            if pattern.search(line):
                fout.write(line)
                kept += 1
    log.info(f"Filtr: {total} → {kept} linii [ERRO]/[CRIT]")
    log.save(output_path)
    return output_path


def preaggregate_logs(input_path: str,
                      output_path: str = PREAGGREG_LOG_PATH) -> str:
    """Grupuje logi w 1h buckety, deduplikuje, skraca frazy,
    zachowuje pierwsze wystąpienie każdego zdarzenia z adnotacją (repeated Nx)."""
    with open(input_path, "r", encoding="utf-8") as f:
        lines = [l.rstrip("\n") for l in f if l.strip()]

    # 1. Grupowanie w buckety godzinowe
    buckets: dict[str, list[str]] = {}
    for line in lines:
        dt = _parse_timestamp(line)
        if dt is None:
            continue
        buckets.setdefault(_bucket_key(dt), []).append(line)
    log.info(f"Pre-agregacja: {len(lines)} linii → {len(buckets)} bucket(ów)")

    # 2. Deduplikacja per bucket — 1 wiersz = 1 unikalne zdarzenie
    result_lines = []
    for key in sorted(buckets.keys()):
        for level in ("ERRO", "CRIT"):
            seen: set[str] = set()
            for bl in buckets[key]:
                lm = _LEVEL_RE.search(bl)
                if lm and lm.group(1) == level:
                    msg = _extract_message(bl)
                    comp = _COMPONENT_RE.search(msg)
                    dedup_key = f"{comp.group(1)}|{msg}" if comp else msg
                    if dedup_key not in seen:
                        seen.add(dedup_key)
                        result_lines.append(f"[{key}] [{level}] {msg}")

    # 3. Skracanie fraz i usuwanie interpunkcji
    shortened = _strip_punctuation(_shorten_phrases("\n".join(result_lines)))
    result_lines = [l for l in shortened.splitlines() if l.strip()]

    # 4. Globalna deduplikacja — zachowaj pierwsze wystąpienie + adnotację (repeated Nx)
    event_positions: dict[str, list[int]] = {}
    for i, line in enumerate(result_lines):
        m = re.match(r"^\[[\d\-: ]+\]\s*(\[(?:ERRO|CRIT)\]\s*.*)$", line)
        event_key = m.group(1) if m else line
        event_positions.setdefault(event_key, []).append(i)

    deduped = []
    seen_events: set[str] = set()
    for i, line in enumerate(result_lines):
        m = re.match(r"^\[[\d\-: ]+\]\s*(\[(?:ERRO|CRIT)\]\s*.*)$", line)
        event_key = m.group(1) if m else line
        if event_key not in seen_events:
            seen_events.add(event_key)
            count = len(event_positions.get(event_key, []))
            if count > 1:
                deduped.append(f"{line} (repeated {count}x total)")
            else:
                deduped.append(line)

    log.info(f"Deduplikacja: {len(result_lines)} → {len(deduped)} linii")

    with open(output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(deduped) + "\n")
    log.save(output_path)
    return output_path


def compress_with_llm(input_path: str,
                      output_path: str = AGGREGATED_LOG_PATH) -> str:
    """Jednokrotna kompresja LLM — zmieszcza logi w TOKEN_LIMIT."""
    with open(input_path, "r", encoding="utf-8") as f:
        content = f.read()
    input_tokens = count_tokens(content)
    log.info(f"Kompresja LLM: {input_tokens} tokenów → max {TOKEN_LIMIT}")
    prompt = AGGREGATE_SYSTEM_PROMPT.format(token_count=input_tokens)
    summary = ask_llm(prompt, content, max_tokens=TOKEN_LIMIT)
    clean = "\n".join(l for l in summary.strip().splitlines() if l.strip())
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(clean + "\n")
    log.info(f"Kompresja: {len(clean.splitlines())} linii")
    log.save(output_path)
    return output_path


def iterative_compress(input_path: str,
                       output_path: str = FINAL_COMPRESSED_PATH) -> str:
    """Iteracyjna kompresja LLM — powtarza aż zmieści się w TOKEN_LIMIT."""
    with open(input_path, "r", encoding="utf-8") as f:
        content = f.read()

    for round_num in range(1, MAX_COMPRESS_ROUNDS + 1):
        current_tokens = count_tokens(content)
        if current_tokens <= TOKEN_LIMIT:
            log.info(f"Mieści się w limicie ({current_tokens}/{TOKEN_LIMIT})")
            break
        ratio = round(current_tokens / TOKEN_LIMIT * 100, 1)
        cut = round((1 - TOKEN_LIMIT / current_tokens) * 100)
        log.info(f"Kompresja runda {round_num}: {current_tokens} → {TOKEN_LIMIT} ({ratio}%, ściąć {cut}%)")
        prompt = FINAL_COMPRESS_PROMPT.format(
            token_count=current_tokens, target=TOKEN_LIMIT, ratio=ratio, cut=cut)
        summary = ask_llm(prompt, content, max_tokens=TOKEN_LIMIT)
        content = "\n".join(l for l in summary.strip().splitlines() if l.strip()) + "\n"

    with open(output_path, "w", encoding="utf-8") as f:
        f.write(content)
    final_tokens = count_tokens(content)
    log.info(f"Iteracyjna kompresja: {final_tokens} tokenów, {len(content.strip().splitlines())} linii")
    log.save(output_path)
    return output_path


def send_answer(task: str, answer) -> dict:
    payload = {"apikey": API_KEY, "task": task, "answer": answer}
    log.info(f"POST {VERIFY_URL} | task={task}")
    log.info(f"  answer = {json.dumps(answer, ensure_ascii=False)[:500]}")
    response = requests.post(VERIFY_URL, json=payload)
    data = response.json()
    log.info(f"  response = {json.dumps(data, ensure_ascii=False)}")
    return data


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    print(f"\n{'='*60}")
    print("  Zadanie 02.03 — Analiza failure.log")
    print(f"{'='*60}\n")

    # 1. Pobierz plik logów
    log.info("Krok 1: Pobieranie pliku logów…")
    filepath = download_log()

    # 2. Analiza rozmiaru
    log.info("Krok 2: Analiza rozmiaru…")
    stats = analyze_log(filepath)
    print(f"\n{SEP}")
    print(f"  {filepath}: {stats['file_size']:,} B, {stats['line_count']:,} linii, {stats['token_count']:,} tokenów")
    print(f"{SEP}\n")

    # 3. Filtrowanie — tylko [ERRO] i [CRIT]
    log.info(f"Krok 3: Filtrowanie [ERRO]/[CRIT]… [{filepath}]")
    filtered_path = filter_log(filepath)

    # 4. Pre-agregacja (buckety 1h + deduplikacja + skróty fraz)
    log.info(f"Krok 4: Pre-agregacja… [{filtered_path}]")
    preaggreg_path = preaggregate_logs(filtered_path)
    with open(preaggreg_path, "r", encoding="utf-8") as f:
        preaggreg_content = f.read()
    pre_tokens = count_tokens(preaggreg_content)
    pre_lines = len(preaggreg_content.strip().splitlines())
    print(f"\n{SEP}")
    print(f"  {preaggreg_path}: {pre_lines} linii, {pre_tokens} tokenów")
    print(f"{SEP}\n")

    # 5. Kompresja jeśli potrzebna
    if pre_tokens <= TOKEN_LIMIT:
        log.info(f"Mieści się w limicie ({pre_tokens}/{TOKEN_LIMIT})")
        answer_text = preaggreg_content.strip()
    else:
        log.info(f"Za dużo ({pre_tokens}/{TOKEN_LIMIT}) — kompresja LLM…")
        agg_path = compress_with_llm(preaggreg_path)
        with open(agg_path, "r", encoding="utf-8") as f:
            agg_content = f.read()
        agg_tokens = count_tokens(agg_content)

        if agg_tokens <= TOKEN_LIMIT:
            answer_text = agg_content.strip()
        else:
            log.info("Iteracyjna kompresja…")
            final_path = iterative_compress(agg_path)
            with open(final_path, "r", encoding="utf-8") as f:
                answer_text = f.read().strip()

    # 6. Podsumowanie i wysyłka
    final_tokens = count_tokens(answer_text)
    print(f"\n{SEP}")
    print(f"  Końcowy wynik: {final_tokens} tokenów, {len(answer_text.splitlines())} linii")
    print(f"{SEP}\n")
    print(answer_text)

    log.info("Krok 6: Wysyłanie odpowiedzi…")
    result = send_answer("failure", {"logs": answer_text})
    print(f"\n{SEP}")
    print(f"  Odpowiedź: {json.dumps(result, ensure_ascii=False)}")
    print(f"{SEP}\n")
