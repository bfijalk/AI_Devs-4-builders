"""
Narzędzia agenta:

fetch_docs(url):
  - Pobiera plik spod podanego URL.
  - Wykrywa referencje do innych plików (np. [include file="..."] lub linki .md/.pdf/.txt).
  - Rekurencyjnie pobiera wszystkie powiązane pliki.
  - Zapisuje wszystko do folderu `files/`.
  - Zwraca podsumowanie pobranych plików.

image_to_docs(image_path):
  - Wczytuje plik graficzny (png/jpg/gif/webp) z dysku.
  - Wysyła go do modelu wizyjnego (gpt-4o vision).
  - Generuje opis i dokumentację w formacie Markdown.
  - Zapisuje wynik jako <nazwa_pliku>.md obok oryginału.
"""

import base64
import os
import re
import urllib.request
import urllib.error
from pathlib import Path
from typing import Optional

from openai import OpenAI
from dotenv import load_dotenv

import log

load_dotenv()

_vision_client = OpenAI(
    api_key=os.getenv("OPEN_ROUTER_API_KEY"),
    base_url=os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1"),
)
_VISION_MODEL = os.getenv("OPEN_ROUTER_MODEL", "gpt-4o")

BASE_URL = "https://hub.ag3nts.org/dane/doc/"
FILES_DIR = Path(__file__).parent / "files"

# Wzorce wykrywania referencji do innych plików
_INCLUDE_RE = re.compile(r'\[include\s+file=["\']([^"\']+)["\']', re.IGNORECASE)
_LINK_RE = re.compile(r'\[(?:[^\]]*)\]\(([^)#][^)]*)\)')
_BARE_URL_RE = re.compile(r'https?://[^\s)"\']+')

_SUPPORTED_EXT = {
    ".md", ".txt", ".pdf", ".json", ".csv", ".html", ".xml",
    ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".tiff",
}


def _filename_from_url(url: str) -> str:
    return url.rstrip("/").split("/")[-1] or "index.md"


def _resolve_url(ref: str, parent_url: str) -> Optional[str]:
    """Zamienia relatywną referencję na pełny URL pod BASE_URL."""
    ref = ref.strip()
    if ref.startswith("http://") or ref.startswith("https://"):
        return ref if any(ref.endswith(ext) for ext in _SUPPORTED_EXT) else None
    ext = Path(ref).suffix.lower()
    if ext not in _SUPPORTED_EXT and ext != "":
        return None
    if ext == "":
        return None
    return BASE_URL + ref


def _extract_refs(content: str, parent_url: str) -> list[str]:
    """Wyciąga wszystkie referencje do zewnętrznych plików z treści dokumentu."""
    found: list[str] = []

    for m in _INCLUDE_RE.finditer(content):
        ref = m.group(1)
        url = _resolve_url(ref, parent_url)
        if url:
            found.append(url)

    for m in _LINK_RE.finditer(content):
        ref = m.group(1)
        if ref.startswith("#"):
            continue
        url = _resolve_url(ref, parent_url)
        if url:
            found.append(url)

    for m in _BARE_URL_RE.finditer(content):
        ref = m.group(0)
        ext = Path(ref.split("?")[0]).suffix.lower()
        if ext in _SUPPORTED_EXT:
            found.append(ref)

    return list(dict.fromkeys(found))


def _fetch_url(url: str) -> Optional[bytes]:
    log.fetch_start(url)
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=15) as resp:
            data = resp.read()
        log.fetch_ok(url, len(data))
        return data
    except Exception as exc:
        log.fetch_error(url, exc)
        return None


def fetch_docs(url: str) -> dict:
    """
    Pobiera dokument spod podanego URL wraz ze wszystkimi powiązanymi plikami.
    Zapisuje je do folderu `files/`. Zwraca listę pobranych plików.
    """
    log.tool_call("fetch_docs", {"url": url})

    FILES_DIR.mkdir(parents=True, exist_ok=True)

    visited: set[str] = set()
    queue: list[str] = [url]
    downloaded: list[str] = []

    while queue:
        current_url = queue.pop(0)
        if current_url in visited:
            continue
        visited.add(current_url)

        data = _fetch_url(current_url)
        if data is None:
            continue

        filename = _filename_from_url(current_url)
        dest = FILES_DIR / filename
        dest.write_bytes(data)
        log.save(str(dest))
        downloaded.append(filename)

        ext = Path(filename).suffix.lower()
        if ext in (".md", ".txt", ".html", ".xml"):
            try:
                text = data.decode("utf-8", errors="replace")
            except Exception:
                continue

            refs = _extract_refs(text, current_url)
            new_refs = [r for r in refs if r not in visited]
            if new_refs:
                log.refs_found(len(new_refs), filename)
                queue.extend(new_refs)

    result = {"downloaded": downloaded, "count": len(downloaded), "folder": str(FILES_DIR)}
    log.tool_result("fetch_docs", result)
    return result


_IMAGE_MIME = {
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
    ".gif": "image/gif",
    ".webp": "image/webp",
}

_IMAGE_DOC_PROMPT = (
    "Jesteś ekspertem od tworzenia dokumentacji technicznej. "
    "Przeanalizuj dokładnie poniższy obraz i wygeneruj szczegółową dokumentację w formacie Markdown. "
    "Dokumentacja powinna zawierać:\n"
    "- Nagłówek z nazwą/tytułem opisywanego elementu\n"
    "- Opis ogólny: co przedstawia obraz\n"
    "- Szczegółowy opis wszystkich widocznych elementów, oznaczeń, wartości i struktur\n"
    "- Wszelkie widoczne dane, tabele, schematy — przepisane do formatu Markdown\n"
    "- Wnioski lub uwagi techniczne, jeśli są zasadne\n"
    "Odpowiedz wyłącznie treścią dokumentu Markdown, bez dodatkowych komentarzy."
)


def image_to_docs(image_path: str) -> dict:
    """
    Wczytuje plik graficzny, analizuje go modelem wizyjnym i generuje dokumentację Markdown.
    Wynikowy plik .md jest zapisywany obok oryginału.
    """
    log.tool_call("image_to_docs", {"image_path": image_path})

    src = Path(image_path)
    if not src.exists():
        result = {"error": f"Plik nie istnieje: {image_path}"}
        log.tool_result("image_to_docs", result)
        return result

    ext = src.suffix.lower()
    mime = _IMAGE_MIME.get(ext)
    if mime is None:
        result = {"error": f"Nieobsługiwany format graficzny: {ext}"}
        log.tool_result("image_to_docs", result)
        return result

    log.info(f"Kodowanie obrazu: {src.name} ({src.stat().st_size} bytes)")
    b64 = base64.b64encode(src.read_bytes()).decode("ascii")
    data_url = f"data:{mime};base64,{b64}"

    log.info(f"Wysyłanie obrazu do modelu wizyjnego ({_VISION_MODEL})…")
    try:
        response = _vision_client.chat.completions.create(
            model=_VISION_MODEL,
            messages=[
                {
                    "role": "user",
                    "content": [
                        {"type": "text", "text": _IMAGE_DOC_PROMPT},
                        {"type": "image_url", "image_url": {"url": data_url}},
                    ],
                }
            ],
            max_tokens=4096,
        )
        md_content = response.choices[0].message.content or ""
    except Exception as exc:
        result = {"error": f"Błąd modelu wizyjnego: {exc}"}
        log.tool_result("image_to_docs", result)
        return result

    out_path = src.with_suffix(".md")
    out_path.write_text(md_content, encoding="utf-8")
    log.save(str(out_path))

    result = {
        "source_image": str(src),
        "output_doc": str(out_path),
        "chars": len(md_content),
    }
    log.tool_result("image_to_docs", result)
    return result


# ── LLM tool definitions & dispatch ───────────────────────────────────────────

DEFINITIONS = [
    {
        "type": "function",
        "function": {
            "name": "fetch_docs",
            "description": (
                "Pobiera dokument spod podanego URL oraz rekurencyjnie wszystkie pliki, "
                "do których ten dokument się odwołuje. Zapisuje je lokalnie w folderze files/. "
                "Użyj gdy chcesz pobrać dokumentację lub sprawdzić powiązane pliki."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "url": {
                        "type": "string",
                        "description": "Pełny URL pliku do pobrania, np. https://hub.ag3nts.org/dane/doc/index.md",
                    },
                },
                "required": ["url"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "image_to_docs",
            "description": (
                "Analizuje plik graficzny (png, jpg, gif, webp) przy użyciu modelu wizyjnego "
                "i generuje z niego dokumentację w formacie Markdown. "
                "Wynikowy plik .md jest zapisywany obok oryginału. "
                "Użyj gdy chcesz opisać lub udokumentować zawartość obrazu."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "image_path": {
                        "type": "string",
                        "description": (
                            "Ścieżka do pliku graficznego na dysku, "
                            "np. files/schemat.png lub /pełna/ścieżka/do/obrazu.jpg"
                        ),
                    },
                },
                "required": ["image_path"],
            },
        },
    },
]

HANDLERS = {
    "fetch_docs": lambda args: fetch_docs(**args),
    "image_to_docs": lambda args: image_to_docs(**args),
}
