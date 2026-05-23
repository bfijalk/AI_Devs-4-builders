from __future__ import annotations

import time

from api_client import download_image, execute_rotations, reset_board
from config import BOARD_URL, TARGET_BOARD, TARGET_URL
from vision import compute_rotations, describe_board


def solve() -> None:
    print("=" * 60)
    print("ELECTRICITY PUZZLE SOLVER (pixel-based)")
    print("=" * 60)

    # 0. Reset board to initial state
    print("\n[0/4] Resetting board…")
    reset_board()

    # 1. Download images
    print("\n[1/4] Downloading current board…")
    current_path = download_image(BOARD_URL, "current_electricity.png", force=True)

    print("\n[2/4] Downloading target board…")
    download_image(TARGET_URL, "solved_electricity.png")
    print(f"  Target (hardcoded): {TARGET_BOARD}")

    # 2. Detect current board via pixel analysis
    print("\n[3/4] Analysing current board (pixel detection)…")
    current_board = describe_board(current_path, "CURRENT")

    # 3. Compare & plan
    print("\n[4/4] Planning rotations…")
    plan, uncertain = compute_rotations(current_board, TARGET_BOARD)

    solved_cells: set[str] = {
        c for c in TARGET_BOARD
        if c not in plan and c not in uncertain
    }
    if solved_cells:
        print(f"  Already correct: {sorted(solved_cells)}")

    if not plan and not uncertain:
        print("\nBoard is already solved — nothing to rotate.")
        return

    if plan:
        execute_rotations(plan)

    # 4. Verify & correct loop
    pass_no = 0
    while True:
        pass_no += 1
        time.sleep(1)

        unsolved = sorted(set(TARGET_BOARD) - solved_cells)
        if not unsolved:
            print(f"\nSUCCESS (after pass {pass_no})! Board matches the target.")
            return

        fresh_path = download_image(BOARD_URL, f"electricity_pass{pass_no}.png", force=True)

        print(f"\n  [Pass {pass_no}] Checking {len(unsolved)} unsolved cell(s): {unsolved}")
        board = describe_board(fresh_path, f"PASS {pass_no}", cells=unsolved)

        remaining, still_uncertain = compute_rotations(board, TARGET_BOARD)

        newly_solved = [
            c for c in unsolved
            if c not in remaining and c not in still_uncertain
        ]
        if newly_solved:
            solved_cells.update(newly_solved)
            print(f"  Confirmed OK: {newly_solved}")

        if not remaining and not still_uncertain:
            print(f"\nSUCCESS (after pass {pass_no})! Board matches the target.")
            return

        if remaining:
            print(f"\n  [Pass {pass_no}] Still needs adjustments: {remaining}")
            execute_rotations(remaining)
