import math
from typing import Optional

from findhim.tools.locations import fetch_power_plants

CITY_COORDINATES: dict[str, dict[str, float]] = {
    "zabrze": {"latitude": 50.3249, "longitude": 18.7857},
    "piotrków trybunalski": {"latitude": 51.4058, "longitude": 19.7028},
    "grudziądz": {"latitude": 53.4837, "longitude": 18.7536},
    "tczew": {"latitude": 53.7781, "longitude": 18.7796},
    "radom": {"latitude": 51.4027, "longitude": 21.1471},
    "chełmno": {"latitude": 53.3494, "longitude": 18.4264},
    "chelmno": {"latitude": 53.3494, "longitude": 18.4264},
    "żarnowiec": {"latitude": 54.7036, "longitude": 18.1367},
    "warszawa": {"latitude": 52.2297, "longitude": 21.0122},
    "kraków": {"latitude": 50.0647, "longitude": 19.9450},
    "gdańsk": {"latitude": 54.3520, "longitude": 18.6466},
    "wrocław": {"latitude": 51.1079, "longitude": 17.0385},
    "poznań": {"latitude": 52.4064, "longitude": 16.9252},
    "łódź": {"latitude": 51.7592, "longitude": 19.4560},
    "szczecin": {"latitude": 53.4285, "longitude": 14.5528},
    "bydgoszcz": {"latitude": 53.1235, "longitude": 18.0084},
    "toruń": {"latitude": 53.0138, "longitude": 18.5984},
    "lublin": {"latitude": 51.2465, "longitude": 22.5684},
    "katowice": {"latitude": 50.2649, "longitude": 19.0238},
    "białystok": {"latitude": 53.1325, "longitude": 23.1688},
}


def get_city_coordinates(city: str) -> Optional[dict]:
    return CITY_COORDINATES.get(city.strip().lower())


def _haversine(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    R = 6371.0
    lat1_r = math.radians(lat1)
    lat2_r = math.radians(lat2)
    dlat = math.radians(lat2 - lat1)
    dlon = math.radians(lon2 - lon1)
    a = math.sin(dlat / 2) ** 2 + math.cos(lat1_r) * math.cos(lat2_r) * math.sin(dlon / 2) ** 2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
    return round(R * c, 2)


def find_nearest_power_plant(latitude: float, longitude: float) -> Optional[dict]:
    plants = fetch_power_plants()
    best = None
    best_distance = float("inf")

    for plant in plants:
        if not plant["is_active"]:
            continue
        coords = get_city_coordinates(plant["city"])
        if coords is None:
            continue
        dist = _haversine(latitude, longitude, coords["latitude"], coords["longitude"])
        if dist < best_distance:
            best_distance = dist
            best = {"city": plant["city"], "code": plant["code"], "distance_km": dist}

    return best


CITY_COORDINATES_TOOL_DEFINITION = {
    "type": "function",
    "function": {
        "name": "get_city_coordinates",
        "description": (
            "Zwraca przybliżone współrzędne geograficzne (latitude, longitude) podanego miasta. "
            "Obsługuje polskie miasta, w tym wszystkie elektrownie z zadania. "
            "Zwraca null jeśli miasto jest nieznane."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "city": {"type": "string", "description": "Nazwa miasta po polsku"},
            },
            "required": ["city"],
            "additionalProperties": False,
        },
        "strict": True,
    },
}

NEAREST_PLANT_TOOL_DEFINITION = {
    "type": "function",
    "function": {
        "name": "find_nearest_power_plant",
        "description": (
            "Dla podanych współrzędnych geograficznych kandydata pobiera listę aktywnych elektrowni, "
            "oblicza odległość do każdej z nich i zwraca tę najbliższą. "
            "Zwraca obiekt z polami: city, code, distance_km."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "latitude": {"type": "number", "description": "Szerokość geograficzna kandydata"},
                "longitude": {"type": "number", "description": "Długość geograficzna kandydata"},
            },
            "required": ["latitude", "longitude"],
            "additionalProperties": False,
        },
        "strict": True,
    },
}

TOOL_DEFINITIONS = [CITY_COORDINATES_TOOL_DEFINITION, NEAREST_PLANT_TOOL_DEFINITION]
