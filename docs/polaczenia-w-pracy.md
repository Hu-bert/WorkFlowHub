# Połączenia w pracy

Jak podłączyć FlowHub do systemów firmowych i co zrobić, gdy czegoś nie widać. FlowHub tylko czyta — niczego w tych systemach nie zmienia.

## 1. Kolejność podłączania

1. **Ustawienia → TFS**: adres kolekcji, token, wersja API `7.0` → „Zapisz i sprawdź”.
2. **Ustawienia → TeamCity / buildy**: źródło „Statusy buildów z TFS” (nie wymaga niczego więcej) albo „TeamCity” (adres + token; daje testy i zmiany).
3. **Ustawienia → Octopus Deploy**: adres, klucz API, potem wybierz z listy środowisko domyślne i środowiska pokazywane.
4. **Ustawienia → Repozytoria lokalne**: folder z kopiami repo (np. `C:\tfs`).
5. **Repozytoria → Katalog repo**: dla każdego repo adres w TFS, gałąź główna (`master`), build configuration z TeamCity i projekt Octopus (listy podpowiadają wartości z serwerów). Repo z dysku dodasz przyciskiem „Dodaj do katalogu”.
6. **Ustawienia → GitHub Copilot CLI**: „Copilot CLI” zamiast atrapy (wcześniej `copilot login` w zwykłym terminalu) oraz plan „Bez limitu”.

## 2. Tokeny

| System | Rodzaj tokenu | Minimalne prawa |
|---|---|---|
| TFS | Personal Access Token (profil → *Security → Personal access tokens*) | Code: Read |
| TeamCity | Access token (profil → *Access Tokens*) | podgląd projektu |
| Octopus | API key (profil → *My API Keys*) | podgląd projektu i wdrożeń |
| Jira Server / Data Center | Personal Access Token | odczyt zadań |
| Jira Cloud | API token + e-mail konta | odczyt zadań |

Tokeny wpisujesz w Ustawieniach — są szyfrowane na Twoim koncie Windows i leżą poza repo. Ścieżki pokazuje karta „Gdzie są zapisane dane”.

## 3. Gdy czegoś nie widać

**Repozytoria → rozwiń repo → zakładka Informacje → „Diagnostyka połączeń”.** Zobaczysz zapytania wysłane do serwerów (bez tokenów) i odpowiedź na każde; „Kopiuj wynik” kopiuje całość.

| Objaw | Najczęstsza przyczyna |
|---|---|
| Brak PR, w diagnostyce `404` | inny projekt albo nazwa repo w TFS niż w adresie repo w katalogu |
| Brak builda | źródło „TeamCity” bez id build configuration przy repo; albo przełącz na „Statusy z TFS” |
| Błąd `400` z TeamCity | serwer nie rozumie filtra — w komunikacie jest odpowiedź serwera, wklej ją |
| Środowiska szare | na środowisku jest inna wersja niż z tego brancha (to informacja, nie błąd) |
| Brak „release z brancha” | FlowHub nie powiązał release z branchem — zobacz punkt 4 |
| Procesy: błąd WinRM | brak dostępu konta do maszyny, wyłączony WinRM albo zapora |

> **Zanim pokażesz wynik diagnostyki komukolwiek (także modelowi AI):** usuń dane, których nie wolno wynosić z firmy — adresy serwerów, nazwy projektów. Sprawdź, czy firmowe zasady na to pozwalają.

## 4. Jak FlowHub łączy systemy

- **Branch → PR:** po nazwie brancha źródłowego w TFS. Branch `pull/<nr>/merge` to PR o tym numerze.
- **Branch → build:** build z brancha oraz build pull requesta (`pull/{id}/merge` — wzorzec zmienisz w Ustawieniach → TeamCity / buildy).
- **Build → release:** release Octopus pasuje do brancha, gdy wskazuje na niego build information albo referencja Git, gdy wersja zawiera nazwę brancha, albo gdy wersja jest w numerze builda (build „1.30.0 #643” → release 1.30.0). Jeśli u Was działa to inaczej, daj znać, jak release powstaje z builda.
- **Repo → procesy:** ręcznie, w edycji repo (maszyna + początek nazwy procesu lub usługi).

## 5. Czego jeszcze nie ma

- **logFaces** — potrzebny opis REST API serwera (rozdział „REST API” z podręcznika albo przykład zapytania).
- Co zostało sprawdzone na prawdziwych serwerach, a co nie: [DECYZJE.md](DECYZJE.md).
