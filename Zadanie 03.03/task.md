1. Napisz funkcje w jezyku c#, ktora pobierze zawartosc strony pod tym url 
https://hub.ag3nts.org/reactor_preview.html
i zachowa jej zawartosc w folderze files.

2. Napisz funkcje, ktora wysle request do api zaczynajac gre

Komendy dla robota wysyłasz do /verify:

{
  "apikey": "tutaj-twoj-klucz",
  "task": "reactor",
  "answer": {
    "command": "start"
  }
}

3. Napisz webhook, ktory pobiera zawartosc strony po wyslaniu kazdego polecenia. Strona zapisana w folderze files zapisuje obecny stan planszy, ktory zmienia sie po kazdym wyslanym requestcie. Chcialbym, zeby stan planszy byl zaktualizowany po kazdej interakcji z api.

4.

Napisz teraz agenta, ktory bedzie realizowal ponizsze zadanie:

Twoim zadaniem jest doprowadzenie robota transportującego urządzenie chłodzące w pobliże reaktora.

Do sterowania robotem służy specjalnie przygotowane API, które przyjmuje polecenia: start, reset, left, wait oraz right. Możesz wysłać tylko jedno polecenie jednocześnie.

Zadanie uznajemy za zaliczone, jeśli robot przejdzie przez całą mapę, nie będąc przy tym zgniecionym przez elementy reaktora. Bloczki reaktora poruszają się w górę i w dół, a status ich aktualnego kierunku, podobnie jak ich pozycja są zwracane przez API.

Napisz aplikację, która na podstawie aktualnej sytuacji na planszy będzie decydowała, jakie kroki powinien podjąć robot.

Dodatkowe instrukcje:

Mechanika zadania
Plansza ma wymiary 7 na 5 pól.

Robot porusza się zawsze po najniższej kondygnacji, czyli jego pozycja startowa to pierwsza kolumna i 5 wiersz.

Miejsce instalacji modułu chłodzenia (Twój punkt docelowy) to 7 kolumna i 5 wiersz (dobrze widać to na podglądzie graficznym podlinkowanym wyżej).

Każdy blok reaktora zajmuje dokładnie 2 pola i porusza się cyklicznie góra/dół. Gdy dojdzie do pozycji skrajnie wysokiej, zaczyna wracać na dół, a gdy osiągnie pozycję najniższą, wraca do góry.

Bloki poruszają się tylko, gdy wydajesz polecenia. Oznacza to, że odczekanie np. 10 sekund nie zmieni niczego na planszy. Jeśli chcesz, aby stan planszy zmienił się bez poruszania robotem, wyślij komendę wait.

Oznaczenia na mapie
P — to pozycja startowa
G — to pozycja do której masz doprowadzić robota
B — to bloki reaktora