1. Wywołaj akcję help na API zmail, żeby poznać wszystkie dostępne akcje i parametry.

2.

Stwórz zestaw narzędzi, które będą mogly byc wykorzystane przez agenta LLM.

Agent powinien miec dostep do takich metod:

Let me read the task.md file and the .env file to understand what needs to be done.

Oto wynik akcji help z API zmail. Dostępne akcje to:

Akcja	Opis
help	Pokazuje dostępne akcje i parametry
getInbox	Zwraca listę wątków w skrzynce. Parametry: page (opcjonalny), perPage (opcjonalny, 5-20)
getThread	Zwraca listę rowID i messageID dla wybranego wątku (bez treści). Parametr: threadID (wymagany)
getMessages	Zwraca jedną lub więcej wiadomości po rowID/messageID. Parametr: ids (wymagany - numeryczny rowID, 32-znakowy messageID lub tablica)
search	Wyszukiwanie wiadomości z operatorami w stylu Gmaila (from:, to:, subject:, "phrase", -exclude, OR, AND). Parametry: query (wymagany), page, perPage
reset	Resetuje licznik requestów dla apikey w memcache
API działa w trybie read-only. Chcesz, żebym kontynuował i przeszukał skrzynkę w poszukiwaniu hasła, daty i kodu potwierdzenia?

Jak komunikować się z API?

Skrzynka mailowa dostępna jest przez API zmail:

POST https://hub.ag3nts.org/api/zmail
Content-Type: application/json

Sprawdzenie dostępnych akcji:

{
  "apikey": "tutaj-twój-klucz",
  "action": "help",
  "page": 1
}

Pobranie zawartości inboxa:

{
  "apikey": "tutaj-twój-klucz",
  "action": "getInbox",
  "page": 1
}

Jak wysłać odpowiedź?

Wysyłasz do /verify:

{
  "apikey": "tutaj-twój-klucz",
  "task": "mailbox",
  "answer": {
    "password": "znalezione-hasło",
    "date": "2026-02-28",
    "confirmation_code": "SEC-tu-wpisz-kod"
  }
}

Gdy wszystkie trzy wartości będą poprawne, hub zwróci flagę {FLG:...}.


Wskazowki:

3. 
Podejście agentowe z Function Calling - to zadanie doskonale nadaje się do pętli agentowej z narzędziami. Agent może mieć do dyspozycji: wyszukiwanie maili, pobieranie treści wiadomości po ID, wysyłanie odpowiedzi do huba i narzędzie do zakończenia pracy. Pętla powinna działać iteracyjnie - szukaj, czytaj, wyciągaj wnioski, szukaj dalej. 

4.
Aktywna skrzynka - skrzynka jest cały czas w użyciu i nowe wiadomości mogą wpływać w trakcie Twojej pracy. Jeśli przeszukałeś całą skrzynkę i nie możesz czegoś znaleźć, warto spróbować ponownie - szukana informacja mogła właśnie dotrzeć. Nie zakładaj od razu, że informacja nie istnieje.


Zadanie:

Spraw aby agent korzystał z wyszukiwarki maili - na podstawie opisu zadania może zbudować odpowiednie zapytania.



Pobierz pełną treść znalezionych wiadomości, żeby przeczytać ich zawartość.



Szukaj informacji po kolei - nie musisz znaleźć wszystkich na raz.



Korzystaj z feedbacku huba, żeby wiedzieć, których wartości jeszcze brakuje lub które są błędne.



Kontynuuj przeszukiwanie skrzynki, aż zbierzesz wszystkie trzy wartości i hub zwróci flagę.



Pamiętaj, że skrzynka jest aktywna - jeśli szukasz czegoś i nie możesz znaleźć, spróbuj ponownie, bo nowe wiadomości mogły dopiero wpłynąć.