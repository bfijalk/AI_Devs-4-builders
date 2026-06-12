1. 

Kod identyfikacyjny elektrowni w Żarnowcu: PWR6132PL

Nazwa zadania: drone

Skąd wziąć dane?

Dokumentacja API drona (HTML):

https://hub.ag3nts.org/dane/drone.html

Mapa poglądowa terenu elektrowni:

https://hub.ag3nts.org/data/tutaj-twój-klucz/drone.png

Mapa jest podzielona siatką na sektory. Przy tamie celowo wzmocniono intensywność koloru wody, żeby ułatwić jej lokalizację.

Zadanie: napisz funkcje python, ktora pobierze te artefaky i zapisze je w pliku files.


2.

Napisz funkcję, która przekaze do LLM'a adres zdjęcia i przeanalizuje, ktory kwadrat pokazuje zdjęcie tamy. Zdjecie jest podzielone na obszary w formacie 4 wiersze i 3 kolumny,
przeanalizuj ktory kwadrat z tego obszaru pokazuje zdjecie tamy. Skup się na miejscu, które jest polaczone z woda. To powinien byc ten element siatki. 

Zanotuj numer kolumny i wiersza sektora z tamą w siatce (indeksowanie od 1).


Zadanie: napisz druga funkcje python, ktora zrealizuje powyzsze zadanie.


3. 



