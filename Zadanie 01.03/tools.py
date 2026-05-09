"""
Tool implementations split into two groups:

COURIER tools (check_package, redirect_package):
  - Handled locally, call hub.ag3nts.org API directly.
  - Registered as LLM function definitions.

UTILITY tools (get_weather, send_email):
  - Delegated to the MCP server over HTTP.
  - Also registered as LLM function definitions.
"""

import os
import json
import asyncio
import urllib.request
import urllib.error
import concurrent.futures
from dotenv import load_dotenv
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

import log

load_dotenv()

_API_KEY = os.getenv("API_KEY", "")
_API_URL = "https://hub.ag3nts.org/api/packages"
MCP_SERVER_URL = os.getenv("MCP_SERVER_URL", "http://localhost:4000/mcp")


# ── Courier tools (local) ──────────────────────────────────────────────────────

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


def check_package(packageid: str) -> dict:
    """Check the status and location of a package (direct API call)."""
    log.tool_call("check_package", {"packageid": packageid})
    result = _call_courier_api({
        "apikey": _API_KEY,
        "action": "check",
        "packageid": packageid,
    })
    log.tool_result("check_package", result)
    return result


def redirect_package(packageid: str, destination: str, code: str) -> dict:
    """Redirect a package to a new destination (direct API call)."""
    log.tool_call("redirect_package", {"packageid": packageid, "destination": destination})
    result = _call_courier_api({
        "apikey": _API_KEY,
        "action": "redirect",
        "packageid": packageid,
        "destination": destination,
        "code": code,
    })
    log.tool_result("redirect_package", result)
    return result


# ── Utility tools (delegated to MCP server) ────────────────────────────────────

def _mcp_call(tool_name: str, arguments: dict) -> dict:
    """Forward a tool call to the remote MCP server via Streamable HTTP."""

    async def _run():
        async with streamable_http_client(MCP_SERVER_URL) as (read, write, _):
            async with ClientSession(read, write) as session:
                await session.initialize()
                result = await session.call_tool(tool_name, arguments)
                text = "\n".join(
                    block.text for block in result.content if hasattr(block, "text")
                )
                return json.loads(text) if text.strip().startswith(("{", "[")) else {"result": text}

    log.mcp_request(tool_name, arguments)
    try:
        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
            result = pool.submit(asyncio.run, _run()).result(timeout=15)
        log.mcp_response(tool_name, result)
        return result
    except Exception as e:
        log.mcp_error(tool_name, e)
        return {"error": str(e)}


def get_weather(location: str) -> dict:
    """Get current weather for a given location (via MCP server)."""
    return _mcp_call("get_weather", {"location": location})


def send_email(to: str, subject: str, body: str) -> dict:
    """Send a short email message (via MCP server)."""
    return _mcp_call("send_email", {"to": to, "subject": subject, "body": body})


# ── LLM tool definitions & dispatch ───────────────────────────────────────────

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
                    "to": {"type": "string", "description": "Recipient email address"},
                    "subject": {"type": "string", "description": "Email subject"},
                    "body": {"type": "string", "description": "Plain-text email body"},
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
