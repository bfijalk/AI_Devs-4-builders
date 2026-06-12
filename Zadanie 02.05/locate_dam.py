"""Punkt 2: lokalizacja sektora tamy na mapie drona."""

import json

from dam_locator import locate_dam_sector

__all__ = ["locate_dam_sector"]


if __name__ == "__main__":
    result = locate_dam_sector()
    print(json.dumps(result, ensure_ascii=False, indent=2))
