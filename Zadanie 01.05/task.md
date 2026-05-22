1. Napisz mi funkcje python, ktora zwroci wynik tego zapytania

Wszystkie żądania to POST na https://hub.ag3nts.org/verify, body jako raw JSON.

Przykład wywołania akcji help:

{
  "apikey": "tutaj-twoj-klucz",
  "task": "railway",
  "answer": {
    "action": "help"
  }
}


2.

To są funkcje, które obsluguje to API

verify({"action": "help"})
verify({"action": "getstatus", "route": "a-1"})
verify({"action": "reconfigure", "route": "a-1"})
verify({"action": "setstatus", "route": "a-1", "value": "RTOPEN"})
verify({"action": "save", "route": "a-1"})

Napisz agenta, ktory bedzie wykonywal te requesty do API. Chcialbym, zeby agent mial dostep do narzedzi, ktore wysla tego typu request. Jezeli akcja sie nie powiedzie, to agent powinien zwrocic kod bledu, ktory wystapil do uzytkownika i zapytac, czy kontynuujemy lub zmieniamy instrukcje.