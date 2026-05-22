import agent_railway as agent


def main() -> None:
    print("Agent Railway — zarządzanie trasami kolejowymi")
    print("Wpisz 'exit' lub 'quit' aby zakończyć.\n")

    history = None

    while True:
        try:
            user_input = input("Ty: ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nDo widzenia!")
            break

        if not user_input:
            continue
        if user_input.lower() in ("exit", "quit"):
            print("Do widzenia!")
            break

        try:
            reply, history = agent.run(user_input, history)
            print(f"\nAgent: {reply}\n")
        except Exception as exc:
            print(f"\n[Błąd]: {exc}\n")


if __name__ == "__main__":
    main()
