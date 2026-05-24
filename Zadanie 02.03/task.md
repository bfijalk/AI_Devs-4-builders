1.

Pobierz pełny plik logów i zapisz go w folderze Files:

https://hub.ag3nts.org/data/tutaj-twój-klucz/failure.log

Plik zmienia się o północy (nowe sygnatury czasu), więc pobieraj go ponownie, jeśli pracujesz na nocną zmianę. 

2.  sprawdź jego rozmiar. Ile ma linii? Ile tokenów zajmuje cały plik?

3. Napisz tool w języku pthon, który przefiltruje ten plik, a następnie stworzy nowy plik filtered_log który będzie zawieral tylko statusy [ERRO] i [CRIT] to powinna byc czysto programistyczna funkcja, bez dostepu do LLMa.


4. Napisz funkcje, ktora tym razem skorzysta z LLMa. Na podstawie pliku filtered_log.log przejrzyj wszystkie logi, które tutaj byly i dokonaj agregacji co pol godziny. Zbierz wszystkie logi, ktore nagromadzily sie przez 60 minut i zamien je na jeden log podsumowujacy zdarzenia z tych 60 minut. Badz scisly. Jezeli bylo kilka rodzajow logow, to spisz podsumowanie dla kazdego typu. Czyli co bylo jako CRIT, co jako Error i co jako warn. W takim przypadku kompresja powinna miec 3 linijki po kazdych 60 minutach. Nowy plik zapisz w folderze files. Zadbaj o to, aby w pliku wynikowym nie bylo pustych linii.

5. Napisz jeszcze jedna funkcje, ktora teraz nie ma dostepu do LLM i deterministycznie policzy, ile ten tekst bedzie mial tokenow.

6. Wyslij odpowiedz

Metodą POST na https://hub.ag3nts.org/verify:

{
  "apikey": "tutaj-twój-klucz",
  "task": "failure",
  "answer": {
    "logs": "[2026-02-26 06:04] [CRIT] ECCS8 runaway outlet temp. Protection interlock initiated reactor trip.\n[2026-02-26 06:11] [WARN] PWR01 input ripple crossed warning limits.\n[2026-02-26 10:15] [CRIT] WTANK07 coolant below critical threshold. Hard trip initiated."
  }
}