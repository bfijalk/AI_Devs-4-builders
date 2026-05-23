import os
from dotenv import load_dotenv

load_dotenv()

API_KEY = os.getenv("API_KEY")
OPEN_ROUTER_API_KEY = os.getenv("OPEN_ROUTER_API_KEY")
OPEN_ROUTER_MODEL = os.getenv("OPEN_ROUTER_MODEL", "gpt-4o")
OPEN_ROUTER_BASE_URL = os.getenv("OPEN_ROUTER_BASE_URL", "https://openrouter.ai/api/v1")

VERIFY_URL = "https://hub.ag3nts.org/verify"
BOARD_URL = f"https://hub.ag3nts.org/data/{API_KEY}/electricity.png"
BOARD_RESET_URL = f"https://hub.ag3nts.org/data/{API_KEY}/electricity.png?reset=1"
TARGET_URL = "https://hub.ag3nts.org/i/solved_electricity.png"

FILES_DIR = "Files"

ALL_CELLS = [f"{r}x{c}" for r in range(1, 4) for c in range(1, 4)]

# Target TBLR — each cell's cable orientation on the solved board.
# Obtained by describing solved_electricity.png once.
TARGET_TBLR: dict[str, str] = {
    "1x1": "0101",
    "1x2": "0111",
    "1x3": "0011",
    "2x1": "1100",
    "2x2": "1101",
    "2x3": "0111",
    "3x1": "1011",
    "3x2": "1010",
    "3x3": "1001",
}
