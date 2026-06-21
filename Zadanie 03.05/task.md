1. Napisz funkcje C#, ktora zbierze informacje o tym, jakie narzedzia sa dostepne dla agenta.

Endpoint: https://hub.ag3nts.org/api/toolsearch

{
  "apikey": "tutaj-twoj-klucz",
  "query": "I need notes about movement rules and terrain"
}

Uwaga: wszystkie narzędzia porozumiewają się tylko w języku angielskim!

Wszystkie znalezione narzędzia obsługuje się identycznie jak toolsearch, czyli wysyła się do nich parametr 'query' oraz własny apikey.

2. Sprobuj pobrac mape terenu na podstawie tego requestu.


{
  "code": 210,
  "message": "Matching tools found.",
  "query": "I need notes about movement rules and terrain",
  "tools": [
    {
      "name": "books",
      "url": "/api/books",
      "description": "Old books and notebooks with various notes.",
      "parameter": "query",
      "score": 7,
      "matched_keywords": [
        "notes",
        "note"
      ]
    },
    {
      "name": "maps",
      "url": "/api/maps",
      "description": "Terrain maps and map-related location resources.",
      "parameter": "query",
      "score": 5,
      "matched_keywords": [
        "terrain"
      ]
    }
  ]
}


3. 

Czy mozesz zwizualzowac output tej mapy tak, aby byl czytelny dla czlowieka?

4. 

Zbierz informacje o tym, jakie auta sa dostepne i jakie sa mechanizmy poruszania sie w tej grze. Wyswietl je bok mapy, tworzac jedno menu kompletujace wszystkie informacje potrzebne do gry.

Wskazówki:

wysłannik musi dotrzeć do miasta Skolwin
pozyskane mapy zawsze mają wymiary 10x10 pól i zawierają rzeki, drzewa, kamienie itp.
masz do dyspozycji 10 porcji jedzenia i 10 jednostek paliwa
każdy ruch spala paliwo (no, chyba że idziesz pieszo) oraz jedzenie. Każdy pojazd ma własne parametry spalania zasobów.
im szybciej się poruszasz, tym więcej spalasz paliwa, ale im wolniej idziesz, tym więcej konsumujesz prowiantu. Trzeba to dobrze rozplanować.
w każdej chwili możesz wyjść z wybranego pojazdu i kontynuować podróż pieszo.
narzędzie toolsearch może przyjąć zarówno zapytanie w języku naturalnym, jak i słowa kluczowe
wszystkie narzędzia zwracane przez toolsearch przyjmują parametr "query" i odpowiadają w formacie JSON, zwracając zawsze 3 najlepiej dopasowane do zapytania wyniki (nie zwracają wszystkich wpisów!)
jeśli dotrzesz do pola końcowego, zdobędziesz flagę i zaliczysz zadanie (flaga pojawi się zarówno na podglądzie, w API jak i w debugu do zadań)