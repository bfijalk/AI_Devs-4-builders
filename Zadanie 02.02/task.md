Kontekst:

Masz do rozwiązania puzzle elektryczne na planszy 3x3 - musisz doprowadzić prąd do wszystkich trzech elektrowni (PWR6132PL, PWR1593PL, PWR7264PL), łącząc je odpowiednio ze źródłem zasilania awaryjnego (po lewej na dole). Plansza przedstawia sieć kabli - każde pole zawiera element złącza elektrycznego. Twoim celem jest doprowadzenie prądu do wszystkich elektrowni przez obrócenie odpowiednich pól planszy tak, aby układ kabli odpowiadał podanemu schematowi docelowemu. Źródłową elektrownią jest ta w lewym-dolnym rogu mapy. Okablowanie musi stanowić obwód zamknięty.

Jedyna dozwolona operacja to obrót wybranego pola o 90 stopni w prawo. Możesz obracać wiele pól, ile chcesz - ale za każdy obrót płacisz jednym zapytaniem do API.

1. Pobierz aktualny stan planszy i zapisz go w folderze files.

2. Pobierz docelowy stan, który znajduje się tutaj https://hub.ag3nts.org/i/solved_electricity.png i zapisz go w folderze files.


3. Napisz funkcję w języku Python, która będzie narzędziem "tool" dołączonym do LLM'a i opisującym w sposób tekstowy obrazek dostarczony w formie PNG.

Notacja TBLR (Top-Bottom-Left-Right):

Każdy kwadrat planszy opisujemy 4-znakowym ciągiem binarnym "TBLR":
  Pozycja 1 (T): 1 jeśli kabel wychodzi górną krawędzią, 0 jeśli nie
  Pozycja 2 (B): 1 jeśli kabel wychodzi dolną krawędzią, 0 jeśli nie
  Pozycja 3 (L): 1 jeśli kabel wychodzi lewą krawędzią, 0 jeśli nie
  Pozycja 4 (R): 1 jeśli kabel wychodzi prawą krawędzią, 0 jeśli nie

Przykłady kształtów:
  ─  rura pozioma         = 0011 (Left + Right)
  │  rura pionowa         = 1100 (Top + Bottom)
  └  zakręt góra-prawo    = 1001 (Top + Right)
  ┘  zakręt góra-lewo     = 1010 (Top + Left)
  ┐  zakręt dół-lewo      = 0110 (Bottom + Left)
  ┌  zakręt dół-prawo     = 0101 (Bottom + Right)
  ├  T-junction           = 1101 (Top + Bottom + Right)
  ┤  T-junction           = 1110 (Top + Bottom + Left)
  ┬  T-junction           = 0111 (Bottom + Left + Right)
  ┴  T-junction           = 1011 (Top + Left + Right)
  ┼  skrzyżowanie         = 1111 (wszystkie)

Mechanika obrotu w notacji TBLR:
  Jeden obrót 90° w prawo: T→R, R→B, B→L, L→T
  Czyli z kodu "TBLR" powstaje nowy kod: T'=L, B'=R, L'=B, R'=T

Adresowanie komórek: ROWxCOLUMN (np. "1x1" = lewy-górny, "3x3" = prawy-dolny)

Docelowy stan planszy (solved) w notacji TBLR:
  1x1: 0011  1x2: 1101  1x3: 1110
  2x1: 0011  2x2: 1101  2x3: 0111
  3x1: 1001  3x2: 1001  3x3: 1010

Następnie przystąp do wykonania zadania.

4. Zadanie:

Reset planszy

Wywołaj GET z parametrem reset, aby zacząć od początku:
https://hub.ag3nts.org/data/tutaj-twój-klucz/electricity.png?reset=1

    1. Odczytaj aktualny stan - pobierz obrazek PNG i opisz go w notacji TBLR (LLM z vision).

    2. Porównaj ze stanem docelowym - programistycznie porównaj kody TBLR i oblicz ile obrotów (0-3) po 90° w prawo potrzebuje każda komórka.

    3. Wyślij obroty - dla każdego pola wymagającego zmiany wyślij odpowiednią liczbę zapytań z polem rotate.

    Każde zapytanie to POST na https://hub.ag3nts.org/verify:

    {
        "apikey": "tutaj-twój-klucz",
        "task": "electricity",
        "answer": {
            "rotate": "2x3"
        }
    }

    4. Sprawdź wynik - pobierz zaktualizowany obrazek, opisz w TBLR, porównaj programistycznie. Powtarzaj aż plansza będzie zgodna z celem.


Dodatkowe wskazówki:
Mechanika obrotów - każdy obrót to 90 stopni w prawo. Żeby obrócić pole "w lewo" (90 stopni w lewo), wykonaj 3 obroty w prawo. Kable na każdym polu mogą wychodzić przez różną kombinację krawędzi (lewo, prawo, góra, dół) - obrót przesuwa je zgodnie z ruchem wskazówek zegara.
Weryfikuj po każdej partii obrotów - po wykonaniu kilku obrotów możesz pobrać świeży obrazek i sprawdzić, czy aktualny stan zgadza się ze schematem. Błędy w interpretacji obrazu mogą skutkować niepotrzebnymi obrotami lub koniecznością resetu.
