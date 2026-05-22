1. Napisz funkcję python, która pobierze plik csv. z tego adresu i zapisze go w foderze Files
https://hub.ag3nts.org/data/tutaj-twój-klucz/categorize.csv


2. Dodatkowo napisz drugą funkcję, ktora skomunikuj sie z hubem

Wysyłasz metodą POST na https://hub.ag3nts.org/verify, osobno dla każdego towaru:

{
  "apikey": "tutaj-twój-klucz",
  "task": "categorize",
  "answer": {
    "prompt": "Czy przedmiot ID {id} jest niebezpieczny? Jego opis to {description}. Odpowiedz DNG lub NEU."
  }
}

3. Dodaj rowniez reset po blednej klasyfikacji lub przekroczeniu budzetu

Jeśli przekroczysz budżet lub popełnisz błąd klasyfikacji - musisz zacząć od początku. Możesz zresetować swój licznik, wysyłając jako prompt słowo reset:

{ "prompt": "reset" }