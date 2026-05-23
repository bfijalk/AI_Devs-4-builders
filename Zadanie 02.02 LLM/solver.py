from __future__ import annotations

import time

from api_client import download_image, reset_board, send_rotation
from config import ALL_CELLS, BOARD_URL, TARGET_TBLR, TARGET_URL
from vision import compute_rotation_for_cell, crop_cells, describe_cell

MAX_ROUNDS_PER_CELL = 6


def solve() -> None:
    print("=" * 60)
    print("ELECTRICITY PUZZLE SOLVER (LLM cell-by-cell)")
    print("=" * 60)

    # 0. Reset
    print("\n[0] Resetting board…")
    reset_board()

    # 1. Download images
    print("\n[1] Downloading images…")
    current_path = download_image(BOARD_URL, "current_electricity.png", force=True)
    download_image(TARGET_URL, "solved_electricity.png")

    # 2. Process each cell sequentially
    print("\n[2] Solving cell by cell…")

    for cell_addr in ALL_CELLS:
        target_tblr = TARGET_TBLR[cell_addr]

        print(f"\n{'='*40}")
        print(f"Cell {cell_addr}  (target: {target_tblr})")
        print(f"{'='*40}")

        for round_no in range(1, MAX_ROUNDS_PER_CELL + 1):
            # Crop current cell
            current_crop = crop_cells(current_path, [cell_addr])[cell_addr]

            # Ask LLM: what TBLR does this cell have?
            print(f"\n  [Round {round_no}] Reading cell…")
            current_tblr = describe_cell(current_crop)

            if not current_tblr:
                print(f"  LLM returned no valid code. Retrying…")
                continue

            print(f"  Current: {current_tblr}  Target: {target_tblr}")

            # Compare in code
            rotations = compute_rotation_for_cell(current_tblr, target_tblr)

            if rotations == 0:
                print(f"  Match! Moving to next cell.")
                break

            if rotations == -1:
                # LLM misread — piece types don't match. Re-read will happen next round.
                print(f"  Mismatch (different piece type) — LLM probably misread. Retrying…")
                continue

            # Apply rotations
            print(f"  Need {rotations} rotation(s). Sending…")
            for _ in range(rotations):
                send_rotation(cell_addr)
                time.sleep(0.5)

            # Fetch fresh board for next round's verification
            time.sleep(0.5)
            current_path = download_image(BOARD_URL, f"verify_{cell_addr}.png", force=True)

            # Re-read the cell to confirm
            print(f"  Verifying…")
            verify_crop = crop_cells(current_path, [cell_addr])[cell_addr]
            verify_tblr = describe_cell(verify_crop)

            if verify_tblr == target_tblr:
                print(f"  Confirmed match after rotation!")
                break

            print(f"  After rotation: {verify_tblr} (expected {target_tblr})")
            # Loop continues to try again

        else:
            print(f"  [WARN] Cell {cell_addr}: could not solve after {MAX_ROUNDS_PER_CELL} rounds")

    print("\n" + "=" * 60)
    print("ALL CELLS PROCESSED!")
    print("=" * 60)
