1. Napisz funkcje w jezyku c#, ktora sprawdzi, jakie mozliwosci ma API.

{
  "apikey": "tutaj-twoj-klucz",
  "task": "windpower",
  "answer": {
    "action": "help"
  }
}

To jest output

=== Windpower API — help ===
{
  "code": 13,
  "message": "Windpower API help",
  "actions": {
    "start": {
      "required": [],
      "description": "Starts a new service window and initializes task state."
    },
    "get": {
      "required": [
        "param"
      ],
      "paramValues": [
        "weather",
        "turbinecheck",
        "powerplantcheck",
        "documentation"
      ],
      "description": "Requests task data. For weather, turbinecheck, and powerplantcheck use getResult to fetch final response. Documentation is returned directly."
    },
    "getResult": {
      "required": [],
      "description": "Returns one completed queued response with sourceFunction field. Retrieved item is removed from queue."
    },
    "config": {
      "requiredSingle": [
        "startDate",
        "startHour",
        "pitchAngle",
        "turbineMode",
        "unlockCode"
      ],
      "requiredBatch": [
        "configs"
      ],
      "description": "Stores scheduling config points. Accepts single point or multiple points in configs. turbineMode: \u0027production\u0027 enables generation, \u0027idle\u0027 disables turbine. unlockCode is required for every point."
    },
    "unlockCodeGenerator": {
      "required": [
        "startDate",
        "startHour",
        "windMs",
        "pitchAngle"
      ],
      "description": "Generates unlockCode signature for given configuration. Result is asynchronous and must be collected with getResult."
    },
    "done": {
      "required": [],
      "description": "Validates final configuration and returns flag on success."
    }
  },
  "notes": [
    "Run start first.",
    "Run turbinecheck before done.",
    "Use getResult for queued outputs."
  ]
}

2. Napisz zestaw narzędzi dla agenta, które mogą być wywolywane rownolegle i asynchronicznie w celu obslugi tego api.


3. Napisz agenta, ktory rozwiaze ponizszy problem:

Odczytaj z prognozy pogody wszystkie momenty, w których wiatr jest bardzo silny i może zniszczyć łopaty wiatraka. Zabezpiecz wtedy turbinę (odpowiednie nachylenie łopat i odpowiedni tryb pracy).

Wyznacz punkt, w którym możliwe jest wygenerowanie brakującej energii i ustaw tam optymalne nachylenie łopat wirnika i poprawny tryb pracy umożliwiający produkcję prądu.

Każda przesłana do API konfiguracja musi być cyfrowo podpisana. Mamy jednak generator kodów, który takie kody dla Ciebie wygeneruje - unlockCodeGenerator, a wygenerowane kody wyślij razem z konfiguracją.

Zapisz konfigurację przez "config".
Na końcu wyślij akcję o nazwie "done", która sprawdzi, czy Twoja konfiguracja jest poprawna.



Kontekst:

Elektrownia nie może pracować przez cały czas, ponieważ jej bateria na to nie pozwoli. Musisz więc uruchomić jej system tylko wtedy, gdy naprawdę będzie wymagany. Jesteś w stanie znaleźć idealny czas poprzez analizę wyników prognozy pogody.

Dostarczone przez nas API dają Ci też informacje na temat stanu samej turbiny oraz na temat wymagań elektrowni. Przygotowanie raportu do każdej z funkcji wymaga czasu. Nie jesteśmy w stanie powiedzieć, ile konkretnie czasu zajmie wykonanie danej funkcji, ale wywołania te są kolejkowane. Później musisz tylko wywołać funkcję do pobierania wygenerowanych raportów.

Każdy wygenerowany raport da się pobrać tylko jednokrotnie. Jeśli uda Ci się wszystko skonfigurować w czasie 40 sekund, to jesteśmy uratowani i możemy przejść do fazy produkcji prądu.

UWAGA: to zadanie posiada limit czasu (40 sekund), w którym musisz się zmieścić. Liniowe wykonywanie wszystkich akcji nie umożliwi Ci ukończenia zadania.

Dodatkowe uwagi

Dodatkowe uwagi





Większość funkcji działa asynchronicznie. Najpierw dodajesz zadanie do kolejki, potem odbierasz wynik przez action "getResult". Odpowiedzi przychodzą w losowej kolejności.

Za wichurę uznajesz wiatr powyżej wytrzymałości wiatraka.
Przy wichurze turbina nie powinna stawiać oporu i nie może produkować prądu.
Przed finalnym "done" musisz wykonać test turbiny przez "turbinecheck".
Każdy punkt konfiguracji musi mieć poprawny unlockCode z funkcji "unlockCodeGenerator".


Zanim przystąpisz do konfiguracji turbiny wiatrowej, musisz uruchomić okno serwisowe poprzez wydanie polecenia:

{
  "apikey": "tutaj-twoj-klucz",
  "task": "windpower",
  "answer": {
    "action": "start"
  }
}

Przykładowe wysłanie konfiguracji może wyglądać tak - w godzinie zawsze ustawiaj minuty i sekundy na zera.

{
  "apikey": "tutaj-twoj-klucz",
  "task": "windpower",
  "answer": {
    "action": "config",
    "startDate": "2238-12-31",
    "startHour": "12:00:00",
    "pitchAngle": 0,
    "turbineMode": "idle",
    "unlockCode": "tutaj-podpis-md5-z-unlockCodeGenerator"
  }
}

Możesz także wysłać wiele konfiguracji za jednym razem - inny format danych.

{
  "apikey": "tutaj-twoj-klucz",
  "task": "windpower",
  "answer": {
    "action": "config",
    "configs": {
      "2026-03-24 20:00:00": {
        "pitchAngle": 45,
        "turbineMode": "production",
        "unlockCode": "tutaj-podpis-1"
      },
      "2026-03-24 18:00:00": {
        "pitchAngle": 90,
        "turbineMode": "idle",
        "unlockCode": "tutaj-podpis-2"
      }
    }
  }
}



Z API porozumiewasz się w ten sposób:

{
  "apikey": "tutaj-twoj-klucz",
  "task": "windpower",
  "answer": {
    "action": "..."
  }
}

Sugerujemy od rozpoczęcia:

