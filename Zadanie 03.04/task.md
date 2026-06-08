1. Pobierz pliki z wiedzą z lokalizacji https://hub.ag3nts.org/dane/s03e04_csv/ zapisz je w folderze files.

2. Przygotuj swoje 1-2 narzędzia, które umożliwią sprawdzenie, które miasto posiada poszukiwane przedmioty. Bądź gotowy, że agent wyśle zapytanie np. jako naturalne zapytanie "potrzebuję kabla długości 10 metrów" zamiast "kabel 10m"

Zadbaj o to, aby narzedzia byly mozliwe do wystawienia za pomoca ngrok. To musza byc serwisy, ktore przyjma zapytania.


3. Zgłoś adresy URL do centrali w ramach zadania i koniecznie dobrze opisz je, aby agent wiedział, kiedy ma ich używać i jakie dane ma im przekazać

Po odpaleniu aplikacji w tym kroku zaczekaj, az podam Ci adres ngrok, ktory przekierowuje ruch zewnetrzny do naszego narzedzia.


4. Agent będzie używał Twoich narzędzi tak długo, aż zgromadzi wszystkie potrzebne informacje niezbędne do stwierdzenia, które miasta posiadają jednocześnie wszystkie potrzebne mu przedmioty


5.

Przykład odpowiedzi:

{
  "apikey": "tutaj-twoj-klucz",
  "task": "negotiations",
  "answer": {
    "tools": [
      {
        "URL": "https://twoja-domena.pl/api/narzedzie1",
        "description": "Opis pierwszego narzędzia - co robi i jakie parametry przyjmuje w polu params"
      },
      {
        "URL": "https://twoja-domena.pl/api/narzedzie2",
        "description": "Opis drugiego narzędzia - co robi i jakie parametry przyjmuje w polu params"
      }
    ]
  }
}

Agent wysyła zapytania POST do Twojego URL w formacie:

{
  "params": "wartość przekazana przez agenta"
}

Oczekiwany format odpowiedzi:

{
  "output": "odpowiedź dla agenta"
}



6. Weryfikacja

Weryfikacja jest asynchroniczna — po wysłaniu narzędzi musisz poczekać kilka sekund, a następnie odpytać o wynik. Zrobisz to wysyłając na ten sam adres /verify zapytanie z polem "action" ustawionym na "check":

{
  "apikey": "tutaj-twoj-klucz",
  "task": "negotiations",
  "answer": {
    "action": "check"
  }
}


7. Agent sam zgłosi do centrali, które miasta znalazł i jeśli będą one poprawne, to otrzymasz flagę



8. Odbierz flagę za pomocą funkcji "check" opisanej wyżej lub odczytaj ją przez narzędzie do debugowania zadań. Pamiętaj, że agent potrzebuje trochę czasu (minimum 30-60 sekund), aby przygotować dla Ciebie odpowiedź

Ograniczenia:
Odpowiedź narzędzia nie może przekraczać 500 bajtów i nie może być krótsza niż 4 bajty
Agent ma do dyspozycji maksymalnie 10 kroków, aby dojść do odpowiedzi
Agent będzie starał się namierzyć miasta dla 3 przedmiotów
Możesz zarejestrować najwyżej 2 narzędzia (ale równie dobrze możesz ogarnąć wszystko jednym)
Jeśli agent nie otrzymał żadnej odpowiedzi od narzędzia, to przerywa pracę

