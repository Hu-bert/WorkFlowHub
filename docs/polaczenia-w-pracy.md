# Połączenia w pracy — jak podłączyć i co sprawdzić

Klienty TFS, TeamCity, Octopus i Jira są napisane wg dokumentacji API i nie były sprawdzone na prawdziwych serwerach. Ten plik mówi, jak je podłączyć i co zrobić, gdy czegoś nie widać.
## 1. Wersje — gdzie je znaleźć

| System | W przeglądarce | Przez API (GET) |
|---|---|---|
| TFS / Azure DevOps Server | ikona „?” → *About* (albo stopka strony administracji) | — (wersję API ustawiasz w Ustawieniach: 6.0 = Server 2020, 7.0 = Server 2022) |
| TeamCity | stopka każdej strony („TeamCity Professional 2024.x”) | `/app/rest/server` → pole `version` |
| Octopus Deploy | menu użytkownika / stopka | `/api` → pole `Version` |
| Jira | stopka strony albo *Administracja → System → Informacje o systemie* | `/rest/api/2/serverInfo` → `version`, `deploymentType` (Server / Cloud) |

Najprościej: wpisz adres i token w **Ustawieniach** FlowHub i kliknij **„Sprawdź połączenie”** — TeamCity pokaże wersję, pozostałe potwierdzą, że adres i token działają.

## 2. Tokeny — jakie i z jakimi prawami

| System | Rodzaj tokenu | Minimalne prawa |
|---|---|---|
| TFS | Personal Access Token (profil → *Security → Personal access tokens*) | Code: Read |
| TeamCity | Access token (profil → *Access Tokens*) | podgląd projektu |
| Octopus | API key (profil → *My API Keys*) | podgląd projektu i wdrożeń |
| Jira Server / Data Center | Personal Access Token (profil → *Personal Access Tokens*) | odczyt zadań |
| Jira Cloud | API token + e-mail konta | odczyt zadań |

FlowHub tylko czyta. Tokeny wpisujesz w Ustawieniach — są szyfrowane na Twoim komputerze i nie trafiają do repo.

## 3. Gdy czegoś nie widać — diagnostyka

Repozytoria → rozwiń wiersz repo → **„Diagnostyka połączeń”**. Zobaczysz listę zapytań, które FlowHub wysłał do serwerów (bez tokenów), i odpowiedź na każde. „Kopiuj wynik” kopiuje całość do schowka.

- `404 — nie znaleziono` przy pull requestach: inna nazwa repo albo projekt w TFS niż w adresie repo w katalogu.
- Brak builda: ustaw źródło buildów „Statusy z TFS” albo wpisz id build configuration przy repo (Repozytoria → Katalog repo → Edytuj).
- Puste środowiska: FlowHub nie dopasował release do brancha — patrz pytanie 2 niżej.

> **Zanim pokażesz wynik komukolwiek (także modelowi AI):** usuń dane, których nie wolno wynosić z firmy — adresy serwerów, nazwy projektów. Sprawdź też, czy firmowe zasady na to pozwalają.
## 4. Trzy pytania, na które API nie odpowie

1. **TeamCity:** jak nazywa się branch w buildach — tak jak w Git (`feature/PROJ-123-opis`), czy skrócony? Numer buildu zawiera nazwę brancha — w jakim formacie?
2. **Octopus:** skąd release „wie”, z którego builda pochodzi — numer wersji = numer buildu? Czy jest osobny kanał / release per branch?
3. **Mapowanie:** czy nazwa repo w TFS = id build configuration = nazwa projektu w Octopus, czy trzeba to wpisać ręcznie w `repos.yaml` (`ciBuildConfigurations`, `deployProject`)?

## 5. Co już jest

| System | Stan w FlowHub |
|---|---|
| TFS | pull requesty, komentarze, polityki, lista repo, **statusy buildów** (źródło buildów „Statusy z TFS”) |
| Jira | treść zadania |
| TeamCity | buildy brancha, testy, zmiany — wymaga id build configuration przy repo (Repozytoria → Katalog → Edytuj) |
| Octopus | release i wdrożenia na środowiska — projekt = nazwa repo albo slug wpisany przy repo |

Żaden klient nie był sprawdzony na prawdziwym serwerze. Najszybsza ścieżka w pracy:

1. Ustawienia → TFS: adres kolekcji, token, „Zapisz i sprawdź”.
2. Ustawienia → TeamCity / buildy: źródło **„Statusy buildów z TFS”** (nie wymaga niczego więcej).
3. Repozytoria → przy repo rozwiń wiersz → **„Diagnostyka połączeń”**. Jeśli PR albo build się nie pokazuje, skopiuj wynik („Kopiuj wynik”) — widać w nim, o co FlowHub zapytał i co odpowiedział serwer (bez tokenów).
4. Dopiero potem TeamCity i Octopus przez REST, jeśli potrzebujesz testów i wdrożeń.