"""Punkt 1: pobieranie i rozpakowywanie danych z czujników."""

from __future__ import annotations

import io
import zipfile
from pathlib import Path
from typing import List, Optional

import requests

from config import FILES_DIR, HTTP_TIMEOUT, SENSORS_ZIP_URL


def download_sensor_data(output_dir: Optional[Path] = None) -> List[Path]:
    """Pobiera sensors.zip z huba i rozpakowuje pliki JSON w folderze files."""
    out_dir = output_dir or FILES_DIR
    out_dir.mkdir(parents=True, exist_ok=True)

    print(f"[GET] {SENSORS_ZIP_URL}")
    response = requests.get(SENSORS_ZIP_URL, timeout=HTTP_TIMEOUT)
    response.raise_for_status()

    extracted: List[Path] = []
    with zipfile.ZipFile(io.BytesIO(response.content)) as archive:
        for member in archive.namelist():
            if member.endswith("/"):
                continue

            target = out_dir / Path(member).name
            target.write_bytes(archive.read(member))
            extracted.append(target)

    print(f"[OK]  Extracted {len(extracted)} files to {out_dir}")
    return extracted


if __name__ == "__main__":
    download_sensor_data()
