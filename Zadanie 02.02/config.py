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

# Hardcoded target board state — detected from solved_electricity.png via pixel analysis
TARGET_BOARD: dict[str, str] = {
    "1x1": "0101",  # ┌  bend (Bottom + Right)
    "1x2": "0111",  # ┬  T-junction (Bottom + Left + Right)
    "1x3": "0011",  # ─  horizontal pipe (Left + Right)
    "2x1": "1100",  # │  vertical pipe (Top + Bottom)
    "2x2": "1101",  # ├  T-junction (Top + Bottom + Right)
    "2x3": "0111",  # ┬  T-junction (Bottom + Left + Right)
    "3x1": "1011",  # ┴  T-junction (Top + Left + Right)
    "3x2": "1010",  # ┘  bend (Top + Left)
    "3x3": "1001",  # └  bend (Top + Right)
}
