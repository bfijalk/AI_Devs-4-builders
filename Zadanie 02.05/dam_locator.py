"""Logika lokalizacji sektora tamy na mapie drona."""

from __future__ import annotations

from pathlib import Path
from typing import Dict, Optional, Tuple, Union

from config import (
    DAM_SECTOR_PATH,
    DRONE_MAP_PATH,
    GRID_COLUMNS,
    GRID_ROWS,
    OPEN_ROUTER_MODEL,
    VISION_MODELS,
    drone_map_url,
    require_api_key,
    require_openrouter_key,
)
from io_utils import resolve_image_url, save_json
from llm_client import OpenRouterClient
from parsing import GridSector, parse_sector_response, validate_sector
from prompts import TEXT_VERIFY_SYSTEM_PROMPT, VISION_SYSTEM_PROMPT, VISION_USER_PROMPT


class DamLocator:
    def __init__(self, llm: Optional[OpenRouterClient] = None, text_model: str = OPEN_ROUTER_MODEL):
        self.llm = llm or OpenRouterClient()
        self.text_model = text_model

    def resolve_image_reference(
        self,
        image: Optional[Union[str, Path]] = None,
        *,
        use_local_file: bool = False,
    ) -> Union[str, Path]:
        if image is not None:
            return image
        if use_local_file:
            return DRONE_MAP_PATH
        require_api_key()
        return drone_map_url()

    def analyze_with_vision(self, image_url: str) -> Tuple[GridSector, str, str]:
        errors = []
        for model in VISION_MODELS:
            print(f"[VISION] Model: {model}")
            try:
                content = self.llm.chat_text(
                    model,
                    messages=[
                        {"role": "system", "content": VISION_SYSTEM_PROMPT},
                        {
                            "role": "user",
                            "content": [
                                {"type": "text", "text": VISION_USER_PROMPT},
                                {"type": "image_url", "image_url": {"url": image_url}},
                            ],
                        },
                    ],
                )
                print(f"[VISION] Odpowiedź: {content}")
                sector = validate_sector(
                    parse_sector_response(content),
                    GRID_COLUMNS,
                    GRID_ROWS,
                )
                return sector, content, model
            except Exception as exc:
                msg = f"{model}: {exc}"
                print(f"[VISION] Błąd — {msg}")
                errors.append(msg)

        raise RuntimeError(
            "Żaden model vision nie zlokalizował sektora tamy.\n" + "\n".join(errors)
        )

    def verify_with_text(
        self,
        sector: GridSector,
        vision_response: str,
        vision_model: str,
    ) -> GridSector:
        user_prompt = (
            f"Model vision ({vision_model}) zanalizował mapę drona i zwrócił:\n"
            f"{vision_response}\n\n"
            f"Wstępne współrzędne: kolumna={sector.column}, wiersz={sector.row}.\n"
            f"Siatka: {GRID_ROWS} wiersze × {GRID_COLUMNS} kolumny. "
            "Zweryfikuj lub popraw te współrzędne."
        )

        print(f"[TEXT] Model: {self.text_model}")
        content = self.llm.chat_text(
            self.text_model,
            messages=[
                {"role": "system", "content": TEXT_VERIFY_SYSTEM_PROMPT},
                {"role": "user", "content": user_prompt},
            ],
        )
        print(f"[TEXT] Odpowiedź: {content}")

        verified = validate_sector(
            parse_sector_response(content),
            GRID_COLUMNS,
            GRID_ROWS,
        )
        if verified != sector:
            print(
                f"[TEXT] Skorygowano: "
                f"({sector.column},{sector.row}) → "
                f"({verified.column},{verified.row})"
            )
        else:
            print("[TEXT] Współrzędne potwierdzone bez zmian")
        return verified

    def save_result(self, sector: GridSector, vision_model: str, vision_response: str) -> Path:
        payload = {
            "column": sector.column,
            "row": sector.row,
            "grid": {"columns": GRID_COLUMNS, "rows": GRID_ROWS},
            "vision_model": vision_model,
            "vision_response": vision_response,
        }
        save_json(DAM_SECTOR_PATH, payload)
        print(f"[OK]  Zapisano wynik: {DAM_SECTOR_PATH}")
        return DAM_SECTOR_PATH

    def locate(
        self,
        image: Optional[Union[str, Path]] = None,
        *,
        use_local_file: bool = False,
        skip_text_verification: bool = False,
    ) -> Dict[str, int]:
        require_openrouter_key()

        image_ref = self.resolve_image_reference(image, use_local_file=use_local_file)
        image_url = resolve_image_url(image_ref)
        print(f"[LLM] Obraz: {image_ref}")

        sector, vision_response, vision_model = self.analyze_with_vision(image_url)
        if not skip_text_verification:
            sector = self.verify_with_text(sector, vision_response, vision_model)

        self.save_result(sector, vision_model, vision_response)
        print(f"[OK]  Sektor tamy: kolumna={sector.column}, wiersz={sector.row}")
        return sector.as_dict()


def locate_dam_sector(
    image: Optional[Union[str, Path]] = None,
    use_local_file: bool = False,
    skip_text_verification: bool = False,
) -> Dict[str, int]:
    """Publiczny entry point dla punktu 2 z task.md."""
    return DamLocator().locate(
        image,
        use_local_file=use_local_file,
        skip_text_verification=skip_text_verification,
    )
