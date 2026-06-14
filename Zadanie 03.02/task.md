1. Napisz funkcje w jezyku c#, ktora wysle ten request do api, a nastepnie wyswietl odpowiedz w logach tak, abym mogl zobaczyc jakie funkcje api sa dla mnie dostene.

Dostęp do maszyny wirtualnej uzyskujesz poprzez API: https://hub.ag3nts.org/api/shell

używasz go w ten sposób:

{
  "apikey": "tutaj-twoj-klucz",
  "cmd": "help"
}

2. Na podstawie tego wyniku napisz funkcje, ktorych bedzie mogl uzyc agenta AI do manewrowania po API.

      ← Response:
      {
        "code": 190,
        "message": "Available commands.",
        "data": [
          "help - show available commands and short descriptions.",
          "ls [path] - list files and directories.",
          "cat \u003Cpath\u003E - show file content (or list directory content).",
          "cd [path] - change current directory.",
          "pwd - print current working directory.",
          "rm \u003Cfile\u003E - remove a file in virtual filesystem.",
          "editline \u003Cfile\u003E \u003Cline-number\u003E \u003Ccontent\u003E - replace one line in a text file.",
          "reboot - rebuild virtual filesystem state from disk.",
          "date - print current server date and time.",
          "uptime - show virtual machine uptime in Linux format.",
          "find \u003Cpattern\u003E - find files by name in the whole virtual filesystem (supports wildcards).",
          "history - show command history.",
          "whoami - print current user name."
        ]
      }

Funkcja powinna byc w stanie zwrocic wynik dzialania tej komendy do agenta, czyli cala odpowiedz z api.
Chodzi o to, aby agent nie wykonywal bezposrednio tych polecen, tylko uzywal zewnetrznych funkcji.


3. Napisz teraz agenta AI, ktory bedzie dzialal w petli probujac wykonac ponizsze dzialanie:

Spróbuj uruchomić plik binarny /opt/firmware/cooler/cooler.bin

    Zdobądź hasło dostępowe do tej aplikacji (zapisane jest w kilku miejscach w systemie)

Agent musi przestrzegac zasad bezpieczenstwa

    pracujesz na koncie zwykłego użytkownika
    nie wolno Ci zaglądać do katalogów /etc, /root i /proc/
    jeśli w jakimś katalogu znajdziesz plik .gitignore to respektuj go. Nie wolno Ci dotykać plików i katalogów, które są tam wymienione.
    Niezastosowanie się do tych zasad skutkuje zablokowaniem dostępu do API na pewien czas i przywróceniem maszyny wirtualnej do stanu początkowego.

Na koncu tego atapu agent powinien wyswietlic zawartosc pliku cooler.bin po odszyfrowaniu

4. Jezeli agent znajdzie plik w ktorym bedzie kod w formacie ECCS-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

Wyslij request do API w sposob opisany w punkcie 5



5. Sposób wysylania odpowiedzi 

Odpowiedź wysyłasz w poniższy sposób do /verify:

{
  "apikey": "tutaj-twoj-klucz",
  "task": "firmware",
  "answer": {
    "confirmation": "uzyskany kod"
  }
}

Kod, którego szukasz, ma format: ECCS-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx