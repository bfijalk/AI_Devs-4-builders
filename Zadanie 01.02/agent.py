import json

from findhim import run_agent, submit_answer

QUESTION = (
    "Pobierz listę osób pracujących w transporcie z Grudziądza. "
    "Dla każdej osoby: sprawdź jej ostatnią lokalizację, użyj narzędzia find_nearest_power_plant "
    "aby znaleźć najbliższą elektrownię i odległość do niej, oraz sprawdź jej poziom dostępu. "
    "Zbierz te dane dla WSZYSTKICH kandydatów. "
    "Następnie spośród wszystkich kandydatów wybierz tę osobę, która jednocześnie: "
    "1. Ma NAJWYŻSZY poziom dostępu. "
    "2. Spośród osób z tym samym najwyższym poziomem dostępu — jest NAJBLIŻEJ jakiejkolwiek elektrowni. "
    "Podsumuj wyniki dla wszystkich osób i wskaż wybranego kandydata z uzasadnieniem."
)

if __name__ == "__main__":
    print(f"Pytanie: {QUESTION}\n")
    answer = run_agent(QUESTION)
    print(f"\nZebrany wynik agenta:\n{json.dumps(answer, indent=2, ensure_ascii=False)}")
    submit_answer(answer)
