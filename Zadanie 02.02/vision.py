from __future__ import annotations

import numpy as np
from PIL import Image


# ── TBLR notation ─────────────────────────────────────────────

def rotate_tblr(code: str) -> str:
    """Rotate a TBLR code 90° clockwise once."""
    t, b, l, r = code[0], code[1], code[2], code[3]
    return f"{l}{r}{b}{t}"


def rotations_needed(current: str, target: str) -> int:
    """Return 0-3 clockwise rotations to match, or -1 if impossible."""
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
    """Group nearby positions, return the position with highest score per group."""
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
    """If fewer than *expected* lines found, reconstruct from known spacing."""
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
    """Return (x_lines, y_lines) — 4 vertical and 4 horizontal grid boundaries."""
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

    if len(x_lines) != 4 or len(y_lines) != 4:
        print(f"  [WARN] Grid detection got x={x_lines}, y={y_lines}; expected 4 each")

    return x_lines, y_lines


def crop_cells(image_path: str, cells: list[str] | None = None) -> dict[str, Image.Image]:
    """Crop individual cells from the board image."""
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


# ── pixel-based TBLR detection ────────────────────────────────

_DARK_THRESHOLD = 160


def _detect_cell_tblr(cell_img: Image.Image) -> str:
    """Detect TBLR code from a cropped cell image by analysing edge pixels."""
    arr = np.array(cell_img.convert("L"))
    h, w = arr.shape

    edge_depth = max(3, int(min(h, w) * 0.12))
    mid_w_s, mid_w_e = int(w * 0.3), int(w * 0.7)
    mid_h_s, mid_h_e = int(h * 0.3), int(h * 0.7)

    t = "1" if np.mean(arr[:edge_depth, mid_w_s:mid_w_e]) < _DARK_THRESHOLD else "0"
    b = "1" if np.mean(arr[-edge_depth:, mid_w_s:mid_w_e]) < _DARK_THRESHOLD else "0"
    l = "1" if np.mean(arr[mid_h_s:mid_h_e, :edge_depth]) < _DARK_THRESHOLD else "0"
    r = "1" if np.mean(arr[mid_h_s:mid_h_e, -edge_depth:]) < _DARK_THRESHOLD else "0"

    return f"{t}{b}{l}{r}"


# ── public API ────────────────────────────────────────────────

def describe_board(image_path: str, label: str = "", cells: list[str] | None = None) -> dict[str, str]:
    """Detect TBLR for each cell using pixel analysis. No LLM needed."""
    cell_images = crop_cells(image_path, cells)
    board = {}

    print(f"\n{'='*60}\nBoard TBLR ({label}) — pixel detection:\n{'='*60}")
    for cell_addr in sorted(cell_images):
        code = _detect_cell_tblr(cell_images[cell_addr])
        board[cell_addr] = code
        print(f"  {cell_addr}: {code}")

    return board


def compute_rotations(
    current: dict[str, str],
    target: dict[str, str],
) -> tuple[dict[str, int], set[str]]:
    """Compare two TBLR board dicts.

    Returns (plan, uncertain) where:
      plan      — {cell: rotations_needed}
      uncertain — cells where detection doesn't match any valid rotation
    """
    plan = {}
    uncertain: set[str] = set()

    cells_to_check = sorted(current.keys() & target.keys())
    for cell in cells_to_check:
        n = rotations_needed(current[cell], target[cell])
        if n > 0:
            plan[cell] = n
        elif n == -1:
            uncertain.add(cell)
            print(f"  [WARN] Cell {cell}: {current[cell]} cannot be rotated to match {target[cell]}")

    if plan:
        print(f"\nRotation plan: {plan}")
    elif not uncertain:
        print("\nNo rotations needed — boards match.")

    return plan, uncertain
