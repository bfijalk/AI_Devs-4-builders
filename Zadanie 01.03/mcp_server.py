"""
MCP server exposing courier and utility tools over HTTP (Streamable HTTP transport).

Run:
    python mcp_server.py

Endpoint: http://localhost:4000/mcp
"""

import os
import json
import urllib.request
import urllib.error
from dotenv import load_dotenv
from mcp.server.fastmcp import FastMCP

load_dotenv()

_API_KEY = os.getenv("API_KEY", "")
_API_URL = "https://hub.ag3nts.org/api/packages"

mcp = FastMCP(
    "Courier Tools",
    host="0.0.0.0",
    port=4000,
    json_response=True,
)


def _call_courier_api(payload: dict) -> dict:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        _API_URL,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return {"error": f"HTTP {e.code}: {e.read().decode()}"}
    except Exception as e:
        return {"error": str(e)}


@mcp.tool()
def check_package(packageid: str) -> dict:
    """Check the current status and location of a courier package."""
    return _call_courier_api({
        "apikey": _API_KEY,
        "action": "check",
        "packageid": packageid,
    })


@mcp.tool()
def redirect_package(packageid: str, destination: str, code: str) -> dict:
    """Redirect a courier package to a new destination. Requires package ID, destination code, and security code."""
    return _call_courier_api({
        "apikey": _API_KEY,
        "action": "redirect",
        "packageid": packageid,
        "destination": destination,
        "code": code,
    })


_WEATHER_DATA = {
    "Kraków": {"temp": -2, "conditions": "snow"},
    "London": {"temp": 8, "conditions": "rain"},
    "Tokyo": {"temp": 15, "conditions": "cloudy"},
    "Grudziądz": {"temp": 3, "conditions": "overcast"},
    "Warsaw": {"temp": 1, "conditions": "partly cloudy"},
    "New York": {"temp": 10, "conditions": "sunny"},
    "Berlin": {"temp": 5, "conditions": "fog"},
}


@mcp.tool()
def get_weather(location: str) -> dict:
    """Get current weather for a given location (city name)."""
    return _WEATHER_DATA.get(location, {"temp": None, "conditions": "unknown"})


@mcp.tool()
def send_email(to: str, subject: str, body: str) -> dict:
    """Send a short email message to a recipient."""
    return {
        "success": True,
        "status": "sent",
        "to": to,
        "subject": subject,
        "body": body,
    }


if __name__ == "__main__":
    print(f"\nMCP Server starting on http://0.0.0.0:4000/mcp")
    print(f"Tools: check_package, redirect_package, get_weather, send_email\n")
    mcp.run(transport="streamable-http")
