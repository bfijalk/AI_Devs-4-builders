from findhim.tools.geo import TOOL_DEFINITIONS as GEO_TOOLS
from findhim.tools.geo import find_nearest_power_plant, get_city_coordinates
from findhim.tools.locations import TOOL_DEFINITIONS as LOCATION_TOOLS
from findhim.tools.locations import fetch_power_plants, get_person_access_level, get_person_location
from findhim.tools.people import TOOL_DEFINITIONS as PEOPLE_TOOLS
from findhim.tools.people import get_transport_people, refresh_people

ALL_TOOLS = PEOPLE_TOOLS + LOCATION_TOOLS + GEO_TOOLS

TOOL_MAP = {
    "get_transport_people":     lambda _: get_transport_people(),
    "refresh_people":           lambda _: refresh_people(),
    "fetch_power_plants":       lambda _: fetch_power_plants(),
    "get_person_location":      lambda a: get_person_location(**a),
    "get_person_access_level":  lambda a: get_person_access_level(**a),
    "get_city_coordinates":     lambda a: get_city_coordinates(**a),
    "find_nearest_power_plant": lambda a: find_nearest_power_plant(**a),
}
