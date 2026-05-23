from __future__ import annotations

import base64
import io
from collections import Counter

import numpy as np
from PIL import Image
from openai import OpenAI

from config import OPEN_ROUTER_API_KEY, OPEN_ROUTER_BASE_URL, OPEN_ROUTER_MODEL

_client = OpenAI(
    api_key=OPEN_ROUTER_API_KEY,
    base_url=OPEN_ROUTER_BASE_URL,
)

MAX_READS = 5

# ── TBLR helpers ──────────────────────────────────────────────

def rotate_tblr(code: str) -> str:
    t, b, l, r = code[0], code[1], code[2], code[3]
    return f"{l}{r}{b}{t}"


def rotations_needed(current: str, target: str) -> int:
    """0-3 clockwise rotations, or -1 if impossible."""
    if len(current) != 4 or len(target) != 4:
        return -1
    c = current
    for n in range(4):
        if c == target:
            return n
        c = rotate_tblr(c)
    return -1


# ── grid detection & cropping ─────────────────────────────────

def _find_groups(positions_scores: list[tuple[int, float]], min_gap: int = 60) -> list[int]:
    lines = []
    group: list[tuple[int, float]] = []
    for pos, score in positions_scores:
        if group and pos > group[-1][0] + min_gap:
            lines.append(max(group, key=lambda x: x[1])[0])
            group = []
        group.append((pos, score))
    if group:
        lines.append(max(group, key=lambda x: x[1])[0])
    return lines


def _complete_lines(lines: list[int], expected: int) -> list[int]:
    if len(lines) >= expected:
        return lines[:expected]
    if len(lines) < 2:
        return lines
    gaps = [lines[i + 1] - lines[i] for i in range(len(lines) - 1)]
    spacing = min(gaps)
    while len(lines) < expected:
        lines.append(lines[-1] + spacing)
    result = [lines[0]]
    for i in range(1, len(lines)):
        gap = lines[i] - result[-1]
        if gap > spacing * 1.5:
            result.append(result[-1] + spacing)
        result.append(lines[i])
    seen = []
    for v in result:
        if not seen or v > seen[-1] + 10:
            seen.append(v)
    return seen[:expected]


def _detect_grid(image_path: str) -> tuple[list[int], list[int]]:
    img = Image.open(image_path).convert("L")
    arr = np.array(img)
    h, w = arr.shape
    x_scores = [(x, (arr[:, x] < 50).sum() / h) for x in range(w)]
    x_candidates = [(x, r) for x, r in x_scores if r > 0.55]
    x_lines = _find_groups(x_candidates)
    if len(x_lines) >= 2:
        x_lo, x_hi = x_lines[0], x_lines[-1]
    else:
        x_lo, x_hi = 0, w
    grid_slice = arr[:, x_lo:x_hi]
    grid_w = grid_slice.shape[1]
    y_scores = [(y, (grid_slice[y, :] < 50).sum() / grid_w) for y in range(h)]
    y_candidates = [(y, r) for y, r in y_scores if r > 0.9]
    y_lines = _find_groups(y_candidates)
    for fallback_thresh in [0.7, 0.5]:
        if len(y_lines) >= 4:
            break
        y_candidates = [(y, r) for y, r in y_scores if r > fallback_thresh]
        y_lines = _find_groups(y_candidates)
    x_lines = _complete_lines(x_lines, 4)
    y_lines = _complete_lines(y_lines, 4)
    return x_lines, y_lines


def crop_cells(image_path: str, cells: list[str] | None = None) -> dict[str, Image.Image]:
    x_lines, y_lines = _detect_grid(image_path)
    img = Image.open(image_path)
    pad = 5
    all_cells = cells or [f"{r}x{c}" for r in range(1, 4) for c in range(1, 4)]
    result = {}
    for cell_addr in all_cells:
        r, c = int(cell_addr[0]), int(cell_addr[2])
        x1 = x_lines[c - 1] + pad
        x2 = x_lines[c] - pad if c < len(x_lines) else img.width - pad
        y1 = y_lines[r - 1] + pad
        y2 = y_lines[r] - pad if r < len(y_lines) else img.height - pad
        result[cell_addr] = img.crop((x1, y1, x2, y2))
    return result


# ── LLM single-cell TBLR read ────────────────────────────────

_CELL_SYSTEM = """\
You see a SINGLE cell from an electricity puzzle board.
It contains a thick black cable connector on a light background.

Determine which edges the cable touches using TBLR notation — 4 binary digits:
  Position 1 (T): 1 if cable reaches the TOP edge, 0 if not
  Position 2 (B): 1 if cable reaches the BOTTOM edge, 0 if not
  Position 3 (L): 1 if cable reaches the LEFT edge, 0 if not
  Position 4 (R): 1 if cable reaches the RIGHT edge, 0 if not

Shapes reference:
  ─  = 0011 (left+right)     │  = 1100 (top+bottom)
  └  = 1001 (top+right)      ┘  = 1010 (top+left)
  ┐  = 0110 (bottom+left)    ┌  = 0101 (bottom+right)
  ├  = 1101 (top+bottom+right)   ┤  = 1110 (top+bottom+left)
  ┬  = 0111 (bottom+left+right)  ┴  = 1011 (top+left+right)

Reply with ONLY the 4 digits, nothing else. Example: 1101
"""


def _pil_to_base64(img: Image.Image) -> str:
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("utf-8")


def _read_cell_tblr(cell_img: Image.Image, temperature: float = 0) -> str | None:
    """Ask LLM for the TBLR code of a single cropped cell."""
    b64 = _pil_to_base64(cell_img)

    response = _client.chat.completions.create(
        model=OPEN_ROUTER_MODEL,
        messages=[
            {"role": "system", "content": _CELL_SYSTEM},
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": "What is the TBLR code? Reply with 4 digits only."},
                    {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}},
                ],
            },
        ],
        max_tokens=20,
        temperature=temperature,
    )

    content = response.choices[0].message.content
    if not content:
        return None
    code = "".join(ch for ch in content.strip() if ch in "01")[:4]
    return code if len(code) == 4 else None


def read_cell_majority(cell_img: Image.Image, rounds: int = MAX_READS) -> str | None:
    """Read a cell multiple times, return most common valid TBLR code."""
    votes: list[str] = []
    for i in range(rounds):
        temp = 0.0 if i == 0 else 0.3
        code = _read_cell_tblr(cell_img, temperature=temp)
        if code:
            votes.append(code)

    if not votes:
        return None

    counter = Counter(votes)
    winner, count = counter.most_common(1)[0]
    print(f"    LLM votes: {votes} -> {winner} ({count}/{len(votes)})")
    return winner


# ── public: describe one cell, compute rotations ──────────────

def describe_cell(cell_img: Image.Image) -> str | None:
    """Describe a single cell crop via LLM majority vote. Returns TBLR or None."""
    return read_cell_majority(cell_img)


def compute_rotation_for_cell(current_tblr: str, target_tblr: str) -> int:
    """Compute rotations needed. Returns 0-3, or -1 if types don't match."""
    return rotations_needed(current_tblr, target_tblr)
