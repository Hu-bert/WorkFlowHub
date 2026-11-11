# Połączenia w pracy — co sprawdzić i co zebrać

Sama wersja serwera **nie wystarczy**, żeby klient API zadziałał za pierwszym razem. Wersja mówi, które adresy są dostępne; do dopasowania pól potrzebne są jeszcze **przykładowe odpowiedzi JSON** z kilku adresów oraz kilka informacji o tym, jak firma łączy systemy ze sobą.

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

## 3. Przykładowe odpowiedzi — skrypt

Skrypt [tools/collect-api-samples.ps1](../tools/collect-api-samples.ps1) robi same zapytania GET i zapisuje odpowiedzi do `api-samples/` (katalog jest w `.gitignore`). Podaj jedno prawdziwe repo, branch z otwartym pull requestem, build configuration, projekt Octopus i klucz zadania:

```powershell
./tools/collect-api-samples.ps1 `
    -TfsCollectionUrl https://tfs.firma.pl/tfs/DefaultCollection -TfsProject Projekt -TfsRepo Billing.Api `
    -TeamCityUrl https://teamcity.firma.pl -TeamCityBuildType Billing_Api_Build `
    -OctopusUrl https://octopus.firma.pl -OctopusProject billing-api `
    -JiraUrl https://jira.firma.pl -JiraIssue PROJ-123 `
    -Branch feature/PROJ-123-opis
```

Systemy, których nie podasz, są pomijane. O tokeny skrypt pyta w oknie (nie zapisuje ich). Błąd, np. 404, też trafia do pliku — to również informacja o serwerze.

> **Zanim pokażesz te pliki komukolwiek (także modelowi AI):** przejrzyj je i usuń albo zamień dane, których nie wolno wynosić z firmy — nazwiska, adresy e-mail, adresy serwerów, treść zadań. Do dopasowania klienta wystarczą nazwy pól i przykładowe wartości. Sprawdź też, czy firmowe zasady w ogóle na to pozwalają.

Adresy TeamCity i Octopus w skrypcie są wg dokumentacji i **nie były sprawdzone na prawdziwym serwerze** — jeśli któryś zwróci błąd, to też jest wynik.

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