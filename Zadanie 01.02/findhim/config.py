import os

from dotenv import load_dotenv
from openai import OpenAI

load_dotenv()

API_KEY: str = os.getenv("API_KEY", "")
MODEL: str = os.getenv("OPEN_ROUTER_MODEL", "gpt-4o")
VERIFY_URL: str = "https://hub.ag3nts.org/verify"

RESULTS_DIR: str = os.path.join(os.path.dirname(__file__), "..", "Results")
PROMPT_FILE: str = os.path.join(os.path.dirname(__file__), "..", "prompt.txt")


def get_llm_client() -> OpenAI:
    return OpenAI(
        api_key=os.getenv("OPEN_ROUTER_API_KEY"),
        base_url=os.getenv("OPEN_ROUTER_BASE_URL"),
    )


def get_pipeline():
    from findhim.pipeline import PeoplePipeline
    return PeoplePipeline(
        api_key=API_KEY,
        llm_client=get_llm_client(),
        prompt_file=PROMPT_FILE,
        results_dir=RESULTS_DIR,
        model=MODEL,
    )
