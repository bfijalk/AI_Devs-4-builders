"""Data layer for loading and querying CSV knowledge base."""

from __future__ import annotations

import csv
import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Item:
    name: str
    code: str


@dataclass(frozen=True)
class City:
    name: str
    code: str


DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "files")


def load_items() -> list[Item]:
    path = os.path.join(DATA_DIR, "items.csv")
    with open(path, encoding="utf-8") as f:
        reader = csv.DictReader(f)
        return [Item(name=row["name"], code=row["code"]) for row in reader]


def load_cities() -> list[City]:
    path = os.path.join(DATA_DIR, "cities.csv")
    with open(path, encoding="utf-8") as f:
        reader = csv.DictReader(f)
        return [City(name=row["name"], code=row["code"]) for row in reader]


def load_connections() -> list[tuple[str, str]]:
    """Returns list of (itemCode, cityCode) tuples."""
    path = os.path.join(DATA_DIR, "connections.csv")
    with open(path, encoding="utf-8") as f:
        reader = csv.DictReader(f)
        return [(row["itemCode"], row["cityCode"]) for row in reader]


class KnowledgeBase:
    """In-memory queryable knowledge base built from CSV files."""

    def __init__(self):
        self._items = load_items()
        self._cities = load_cities()
        self._connections = load_connections()

        self._item_by_code: dict[str, Item] = {i.code: i for i in self._items}
        self._city_by_code: dict[str, City] = {c.code: c for c in self._cities}

        self._cities_by_item: dict[str, set[str]] = {}
        for item_code, city_code in self._connections:
            self._cities_by_item.setdefault(item_code, set()).add(city_code)

    @property
    def items(self) -> list[Item]:
        return self._items

    @property
    def item_names(self) -> list[str]:
        return [i.name for i in self._items]

    def find_cities_for_item(self, item_code: str) -> list[str]:
        """Return list of city names that have the given item."""
        city_codes = self._cities_by_item.get(item_code, set())
        return [
            self._city_by_code[code].name
            for code in city_codes
            if code in self._city_by_code
        ]

    def find_cities_for_items(self, item_codes: list[str]) -> list[str]:
        """Return city names that have ALL given items."""
        if not item_codes:
            return []

        city_sets = []
        for code in item_codes:
            city_codes = self._cities_by_item.get(code, set())
            city_sets.append(city_codes)

        common_cities = set.intersection(*city_sets) if city_sets else set()
        return [
            self._city_by_code[code].name
            for code in common_cities
            if code in self._city_by_code
        ]

    def get_item_by_code(self, code: str) -> Item | None:
        return self._item_by_code.get(code)
