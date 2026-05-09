"""
Tool implementations: check_package, redirect_package, get_weather, send_email.

Courier tools call the external API at hub.ag3nts.org/apidb.
Weather and email are simple local implementations.
"""

import os
import json
import urllib.request
import urllib.error
from dotenv import load_dotenv

import log

load_dotenv()

_API_KEY = os.getenv("API_KEY", "")
_API_URL = "https://hub.ag3nts.org/api/packages"


def _call_api(payload: dict) -> dict:
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


def check_package(packageid: str) -> dict:
    """Check the status and location of a package."""
    log.tool_call("check_package", {"packageid": packageid})
    result = _call_api({
        "apikey": _API_KEY,
        "action": "check",
        "packageid": packageid,
    })
    log.tool_result("check_package", result)
    return result


def redirect_package(packageid: str, destination: str, code: str) -> dict:
    """Redirect a package to a new destination."""
    log.tool_call("redirect_package", {"packageid": packageid, "destination": destination})
    result = _call_api({
        "apikey": _API_KEY,
        "action": "redirect",
        "packageid": packageid,
        "destination": destination,
        "code": code,
    })
    log.tool_result("redirect_package", result)
    return result


_WEATHER_DATA = {
    "Kraków": {"temp": -2, "conditions": "snow"},
    "London": {"temp": 8, "conditions": "rain"},
    "Tokyo": {"temp": 15, "conditions": "cloudy"},
    "Grudziądz": {"temp": 3, "conditions": "overcast"},
    "Warsaw": {"temp": 1, "conditions": "partly cloudy"},
    "New York": {"temp": 10, "conditions": "sunny"},
    "Berlin": {"temp": 5, "conditions": "fog"},
}


def get_weather(location: str) -> dict:
    """Get current weather for a given location."""
    log.tool_call("get_weather", {"location": location})
    result = _WEATHER_DATA.get(location, {"temp": None, "conditions": "unknown"})
    log.tool_result("get_weather", result)
    return result


def send_email(to: str, subject: str, body: str) -> dict:
    """Send a short email message to a recipient."""
    log.tool_call("send_email", {"to": to, "subject": subject})
    result = {
        "success": True,
        "status": "sent",
        "to": to,
        "subject": subject,
        "body": body,
    }
    log.tool_result("send_email", result)
    return result


DEFINITIONS = [
    {
        "type": "function",
        "function": {
            "name": "check_package",
            "description": (
                "Check the current status and location of a courier package. "
                "Use when the operator asks about a package status, location, or tracking."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "packageid": {
                        "type": "string",
                        "description": "The package ID, e.g. PKG12345678",
                    },
                },
                "required": ["packageid"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "redirect_package",
            "description": (
                "Redirect a courier package to a new destination. "
                "Requires the package ID, destination code, and a security code."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "packageid": {
                        "type": "string",
                        "description": "The package ID, e.g. PKG12345678",
                    },
                    "destination": {
                        "type": "string",
                        "description": "Destination warehouse or address code, e.g. PWR3847PL",
                    },
                    "code": {
                        "type": "string",
                        "description": "Security code authorizing the redirect",
                    },
                },
                "required": ["packageid", "destination", "code"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_weather",
            "description": "Get current weather for a given location.",
            "parameters": {
                "type": "object",
                "properties": {
                    "location": {
                        "type": "string",
                        "description": "City name, e.g. Kraków, London, Tokyo",
                    },
                },
                "required": ["location"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "send_email",
            "description": "Send a short email message to a recipient.",
            "parameters": {
                "type": "object",
                "properties": {
                    "to": {
                        "type": "string",
                        "description": "Recipient email address",
                    },
                    "subject": {
                        "type": "string",
                        "description": "Email subject",
                    },
                    "body": {
                        "type": "string",
                        "description": "Plain-text email body",
                    },
                },
                "required": ["to", "subject", "body"],
            },
        },
    },
]

HANDLERS = {
    "check_package": lambda args: check_package(**args),
    "redirect_package": lambda args: redirect_package(**args),
    "get_weather": lambda args: get_weather(**args),
    "send_email": lambda args: send_email(**args),
}
