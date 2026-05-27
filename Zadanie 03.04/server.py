"""Flask API server exposing tools for the agent."""

import logging

from flask import Flask, request, jsonify

from data_store import KnowledgeBase
from llm_search import search_items

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)

app = Flask(__name__)
kb = KnowledgeBase()

logger.info("Knowledge base loaded: %d items, %d cities", len(kb.items), len(kb._cities))


@app.route("/api/search_items", methods=["POST"])
def handle_search_items():
    """
    Search for items matching a natural language description.
    Accepts: {"params": "natural language query"}
    Returns: {"output": "item1 [CODE1], item2 [CODE2], ..."}
    """
    data = request.get_json(force=True)
    query = data.get("params", "")
    logger.info("search_items called with: %s", query)

    if not query:
        return jsonify({"output": "Error: empty query"}), 400

    results = search_items(query, kb)

    if not results:
        output = "No matching items found."
    else:
        parts = [f"{r['name']} [code: {r['code']}]" for r in results]
        output = ", ".join(parts)

    if len(output.encode("utf-8")) > 500:
        parts = [f"{r['code']}" for r in results]
        output = "Matching item codes: " + ", ".join(parts)

    if len(output.encode("utf-8")) > 500:
        output = output[:496] + "..."

    logger.info("search_items response (%d bytes): %s", len(output.encode("utf-8")), output)
    return jsonify({"output": output})


@app.route("/api/find_cities", methods=["POST"])
def handle_find_cities():
    """
    Find cities that have specific items (by item codes).
    Accepts: {"params": "CODE1, CODE2, CODE3"}
    Returns: {"output": "City1, City2, ..."}
    """
    data = request.get_json(force=True)
    params = data.get("params", "")
    logger.info("find_cities called with: %s", params)

    if not params:
        return jsonify({"output": "Error: empty params"}), 400

    item_codes = [code.strip() for code in params.split(",") if code.strip()]

    invalid_codes = [c for c in item_codes if kb.get_item_by_code(c) is None]
    if invalid_codes:
        output = f"Unknown item codes: {', '.join(invalid_codes)}"
        logger.info("find_cities response: %s", output)
        return jsonify({"output": output})

    cities = kb.find_cities_for_items(item_codes)

    if not cities:
        output = "No city has all these items simultaneously."
    else:
        output = ", ".join(sorted(cities))

    if len(output.encode("utf-8")) > 500:
        output = output[:496] + "..."

    logger.info("find_cities response (%d bytes): %s", len(output.encode("utf-8")), output)
    return jsonify({"output": output})


@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok", "items_count": len(kb.items)})


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5050, debug=False)
