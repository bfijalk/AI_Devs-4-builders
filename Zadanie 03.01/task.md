1. Pobierz dane z sensowrow w formacie zip i rozpakuj ten plik lub pliki w folderze files. https://hub.ag3nts.org/dane/sensors.zip

Zadanie: napisz funkcje python realizujaca powyzszy krok

2. Napisz funkcje python, ktora przejrzy te wszystkie pliki i sprawdzi, czy wartosci w pliku znajduja sie pomiedzy wartosciami okreslonymi jako poprawne.
Jezeli wartosci sa niepoprawne, to dodaj ten plik do listy niepoprawnych plikow.

Lista niepoprawnych plikow powinna byc oddzielnym plikiem tekstowym niepoprawne.txt, ktory zawiera nazwy niepoprawnych plikow. 

Zrob to deterministycznie bez uzycia LLM



Informacje:

Każdy czujnik zwraca dane w poniższym formacie:

{
  "sensor_type": "temperature/voltage",
  "timestamp": 1774064280,
  "temperature_K": 612,
  "pressure_bar": 0,
  "water_level_meters": 0,
  "voltage_supply_v": 230.4,
  "humidity_percent": 0,
  "operator_notes": "Readings look stable and within expected range."
}

Format danych w pojedynczym pliku JSON:





sensor_type — nazwa aktywnego sensora lub zestawu sensorów rozdzielonych znakiem /, np. temperature, water, voltage/temperature



timestamp — unixowy znacznik czasu



temperature_K — odczyt temperatury w Kelwinach



pressure_bar — odczyt ciśnienia w barach



water_level_meters — odczyt poziomu wody w metrach



voltage_supply_v — odczyt napięcia zasilania w V



humidity_percent — odczyt wilgotności w procentach



operator_notes — notatka operatora po angielsku

W każdym pliku obecne są wszystkie pola pomiarowe. Dla sensorów nieaktywnych wartość powinna być ustawiona na 0.

Zakres poprawnych wartości dla aktywnych sensorów:





temperature_K: od 553 do 873



pressure_bar: od 60 do 160



water_level_meters: od 5.0 do 15.0



voltage_supply_v: od 229.0 do 231.0



humidity_percent: od 40.0 do 80.0


3.

Napisz teraz funkcje python ktora z zbierze zawartosc plikow z pliku niepoprawne.txt i stworzy plik niepoprawne_content.json, ktory bedzie zawieral zmergowana zawartosc wszystkich tych plikow, ktore tam sa. Chcialbym miec jeden wynikowy plik, ktory bedzie mial wszystkie te statystki.

4. Napisz teraz funkcje, ktora zestawi ze sobą wszystkie komunikaty wysylane przez operatora z opisanych plikow. Chcialbym, zeby wynikiem byl numer pliku i komentarz operatora. Ta czesc powinna zostac utworzona w pythonie w sposob deterministyczny, a jej wynikiem powinien byc jeden plik, ktory zawiera numer pliku i komentarz w nim zawarty.

Nastepnie dokonaj analizy za pomoca LLM tego jednego pliku, w ktorym jest polaczenie numeru pliku i komentarz operator zglasza bledy. Wynikiem tego dzialania powinien byc plik niepoprawne_llm. Zawierajace numery plikow, ktore operator zglosil jako niepoprawne. LLM powinien sprawdzic, ktore komentarze mowia o bledach i zwrocic tylko numer plikow. 

Nie wysylaj plikow pojedyczno, to powinno byc jedno zapytanie do llm'a.



5. Przeanalizuj teraz wyniki, ktore dostarczyl LLM. Jezeli operator okreslil plik jako niedzialajacy, a dane sprawdzone w sposob deterministyczny sie zgadzaja, to dodaj numer pliku do pliku wynikowego, ktory zawiera bledne pliki.

Odpowiedź wysyłasz do Centrali do /verify w formacie jak poniżej:

{
  "apikey": "tutaj-twoj-klucz",
  "task": "evaluation",
  "answer": {
    "recheck": ["0001","0002","0003", "..."]
  }
}