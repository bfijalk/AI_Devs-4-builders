1. Napisz funkcje w jezyku c#, ktora udokumentuje to API. Dokumentacje zapisz w folderze files

Na początek zacznij od zapoznania się z API dostępnym pod /verify w Centrali.

{
  "apikey": "tutaj-twoj-klucz",
  "task": "okoeditor",
  "answer": {
    "action": "help"
  }
}

2.

Napisz funkcje w jezyku c#, mozesz wykorzystac do tego playwright, ktora zaloguje sie do ponizej podanej strony wykorzystujac te dane

Zdobyliśmy login i hasło do wejścia do tego systemu, ale nie wolno Ci wprowadzać tam żadnych ręcznych zmian. Cała edycja musi odbywać się przez nasze tylne wejście.

Zadanie nazywa się: okoeditor

Nasze API jest dostępne standardowo pod adresem /verify

Panel webowy operatora: https://oko.ag3nts.org/

Login: Zofia
Hasło: Zofia2026!
Klucz: Twój apikey

Po zalogowaniu sie do tej strony agent powinien byc w stanie wyszukac i pobrac dowolony raport wskazany przez uzytkownika. (Zaczekaj na input, ktory przyjdzie od uzytkownika). Input powinien przyjsc w terminalu.

Po pobraniu raportu agent nie kończy pracy — przechodzi od razu do menu edycji wybranego raportu (patrz punkt 3).

Artefakty powinny zostac zapisane w folderze files.

Agent powinien być w stanie dynamicznie pobrać elementy, ktore sa na stronie. Nic nie hardcoduj


3. Napisz agenta, który przyjmie input od uzytkownika i na jego podstawie wykona operacje na api, biorac pod uwage wybrany przez uzytkownika raport.

Agent uruchamia się automatycznie po pobraniu raportu w punkcie 2 albo samodzielnie przez `dotnet run 3` (wtedy zaczyna od menu głównego).

Agent powinien miec dostep do:
-narzedzi playwright, aby moc pobrac raport i zrozumiec, co w tym raporcie sie znajduje
-narzedzi do wywolania api, aby moc wprowadzic zaproponowane przez siebie zmiany.

Po wczytaniu raportu uzytkownik powinien miec mozliwosc
-wykonania zmian na obecnym raporcie, ktore sam opisuje
-zatwierdzenia zmian, ktore zostaly przygotowane przez llm
-cofniecia sie do glownego menu w celu pracy na innym pliku
-wyslania requestu "done" do api, informujac o zakonczeniu zadania.

Wprowadzane zmiany powinny zmylic operatora i nie dawac poznac, ze cos jest nie tak. Edycje musza wygladac naturalnie — jak zwykla korekta lub uzupelnienie raportu, a nie jawna manipulacja. Agent (i LLM) nie powinien dodawac do tresci sygnalow, meta-komentarzy ani sformulowan, ktore ujawniaja prawdziwy cel operacji.

## Plan zaliczenia (done)

Przed wysłaniem `done` agent powinien dynamicznie zweryfikować stan wpisów (Playwright + katalog):

1. **Kod incydentu** — tytuł musi zaczynać się od `MOVE00`, `PROB00` lub `RECO00` (reguły w notatce „Metody kodowania incydentów”).
2. **Skolwin** — wpisy powiązane ze Skolwinem muszą mieć dokładne słowo `Skolwin` w tytule (forma „Skolwina” nie wystarcza).
3. **Zwierzęta** — jeśli treść opisuje zwierzęta, kod incydentu powinien być `MOVE04`, nie `MOVE03`.
4. **Styl operatora** — bez meta-komentarzy („zmieniono klasyfikację”), bez agresywnych sformułowań (np. o niszczycielach), bez ujawniania intencji operacji.
5. **Dywersja** — utworzyć wiarygodny incydent odwracający uwagę (np. ruch w **Komarowie**), opisany jak standardowy meldunek terenowy.
6. **Done** — dopiero po przejściu weryfikacji wysłać `action: "done"`. Przy błędzie `-720` poprawić wpisy Skolwina i ponowić.

W menu agenta: opcja **„Sprawdź gotowość do done”** uruchamia wstępną walidację; **„Wyślij done”** wysyła request bez dodatkowej walidacji.

## Tryb autonomiczny

Agent może samodzielnie zrealizować plan operacji, wczytując listę zadań z pliku **`autonomus.md`** w katalogu projektu.

Uruchomienie:
- `dotnet run 4` — tryb autonomiczny z domyślnym plikiem `autonomus.md`
- `dotnet run 4 <ścieżka>` — tryb autonomiczny z własnym plikiem planu
- menu główne agenta (opcja 4) — to samo z poziomu `dotnet run 3`

Agent sam wybiera wpisy z katalogu, wprowadza zmiany przez API, weryfikuje gotowość i wysyła `done`.

