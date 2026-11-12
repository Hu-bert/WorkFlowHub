# Decyzje i stan sprawdzenia

Co zdecydowałem sam podczas pracy autonomicznej (każdą decyzję można cofnąć) i — osobno — co zostało sprawdzone na prawdziwych systemach, a co nie. Historia zmian krok po kroku jest w Gicie (`git log --first-parent main`).

## Co jest sprawdzone, a co nie

| Obszar | Stan |
|---|---|
| Copilot CLI 1.0.109 | **sprawdzone**: krok flow w aplikacji (zgoda w UI, agent, skill, plik wynikowy, tokeny), terminal, generator bloczków, wznowienie sesji (`--resume`) |
| TFS (Azure DevOps Server 25H2) | **działa u użytkownika**: pull request z recenzentami i komentarzami. Niesprawdzone: polityki brancha, lista repo do wyboru, statusy buildów z TFS |
| TeamCity 2026.1 | **działa u użytkownika**: build brancha z listą nieudanych testów, lista build configuration. Filtr buildów poprawiony po błędzie 400 (bez `running`) |
| Octopus 2026.2 | **działa u użytkownika**: lista środowisk i projektów. Niesprawdzone: dopasowanie release do brancha, wersje na środowiskach po zmianie z fazy 14 |
| Jira | niesprawdzone (klient wg dokumentacji REST v2) |
| Procesy na maszynach | sprawdzone lokalnie; zdalna maszyna przez WinRM — niesprawdzona |
| Klonowanie repo | sprawdzone na lokalnym repo; z TFS zależy od danych logowania zapisanych w Gicie |
| Mapa repo | sprawdzona na podstawionym agencie; aktualizacja prawdziwym agentem — nie |
| Pętla „co dalej?” | sprawdzona na atrapie agenta; z prawdziwym agentem zmieniającym kod — nie |
| Linki do TeamCity / Octopus / TFS | format adresów nie był potwierdzony w dokumentacji (parametr `branch`, ścieżki portalu Octopus, `?version=GB…`) |
| Przeciąganie kart na kanbanie | logika sprawdzona zdarzeniami; gest prawdziwą myszą — nie |

Gdy coś nie działa z serwerem: Repozytoria → rozwiń repo → „Diagnostyka połączeń” → „Kopiuj wynik”.

## Podstawy

| Decyzja | Dlaczego |
|---|---|
| **.NET 10 (LTS)**, paczki Microsoft.* 10.0.x, `FlowHub.slnx`, Microsoft.Testing.Platform | Spec mówi o LTS; .NET 10 SDK nie wspiera VSTest dla xUnit v3. |
| **Vuetify 4** zamiast PrimeVue | PrimeVue 5 jest komercyjne. Vuetify: MIT, aktywnie rozwijane, ikony MDI działają bez internetu. |
| **Baza przez migracje EF** (`dotnet-ef` jako narzędzie lokalne) | Zmiany schematu bez kasowania danych. |
| **Skrypty `.ps1` z BOM**, kontekst kroku w zmiennych `FLOWHUB_*` | Windows PowerShell 5.1 psuje polskie znaki bez BOM. |

## Flow i silnik

| Decyzja | Dlaczego |
|---|---|
| **Repo wybierasz przy tworzeniu zadania** („Zaproponuj” dopasowuje po słowach kluczowych, 0 tokenów) | Prostsze niż osobny krok agenta; i tak zatwierdzasz. |
| **Flow rusza po „Uruchom flow”**, kroki idą po kolei; proste skrypty bez akceptacji | Przewidywalność. |
| **Krok nieobsługiwany albo usunięty z flow** czeka z opisem problemu zamiast wyjątku | Zadanie nie utyka bez wyjaśnienia. |
| **Krok `Check: pull-request`** przed wiadomością na Teams, zapisuje `pr-<repo>.md` | Wiadomość potrzebuje linku do PR. PR tworzysz sam. |
| **„Co dalej?” (`followUp`)**: polecenie z kroku ręcznego uruchamia agenta bez drugiego zatwierdzania; kroki pomiędzy idą jeszcze raz | Sam to zleciłeś; zgoda na `write` / `shell` dalej obowiązuje. |
| **Powtórka kroku z uwagą kontynuuje sesję Copilota** (`--resume`) | Agent pamięta, co zrobił — taniej i trafniej. |
| **Mapa repo (`remember: true`)**: wynik analizy trafia do `knowledge/<repo>.md` z commitem; kolejne zadanie dostaje ją w pliku wynikowym z poleceniem „zaktualizuj od commita X” | Żeby nie analizować repo od zera. Nieudana analiza nie nadpisuje mapy. |
| **Zapis z edytora flow przepisuje YAML bez komentarzy** | Serializator ich nie zachowuje; historia jest w Gicie. |
| **Propozycje z retro wdrażasz ręcznie** w edytorze bloczków | Zasada: zmiany w `blocks/` zawsze jako szkic. |
| **Generator bloczków**: opis + wzorzec tego samego rodzaju → szkic w edytorze; agent bez narzędzi, w pustym katalogu | Nic nie zapisuje się bez „Zapisz”. Na atrapie zwraca oznaczoną kopię wzorca. |

## Copilot i tokeny

| Decyzja | Dlaczego |
|---|---|
| **Runner przez Copilot CLI** (`-p`, JSONL), nie SDK | CLI wystarcza; SDK można dodać jako drugi `IAgentRunner`. |
| **Zgoda w UI przed sesją z `write` / `shell`**; plik wynikowy przez `--allow-tool write(<pełna ścieżka>)` | W trybie `-p` CLI nie pyta — odrzuca narzędzia spoza listy. |
| **Agenci mają `edit` we frontmatterze** | Lista `tools` ogranicza narzędzia; bez `edit` agent nie zapisze pliku wynikowego. |
| **Tokeny z `--usage-output-file`** po zakończeniu sesji; przekroczony budżet daje wtedy ostrzeżenie, nie błąd | CLI nie podaje tokenów w strumieniu. Do budżetu liczymy wejście bez odczytów z cache. |
| **Poziomy modeli domyślnie `auto`, bez cennika**; kredyty AI (`totalNanoAiu`) nie są przeliczane na pieniądze | Na koncie były inne modele niż przykładowe. |
| **Plan „bez limitu”** wyłącza budżety i wycenę | Copilot Enterprise. |
| **Lista modeli = `auto` + modele widziane w sesjach** (`models.json`) | CLI nie ma polecenia wypisującego modele ani planu konta poza sesją. |
| **Terminal: xterm.js ↔ WebSocket ↔ ConPTY** (własne wywołania Win32) | Pełny interaktywny CLI bez własnej nakładki. Serwer uruchamia tylko skonfigurowane CLI w katalogu zadania / repo; WebSocket przyjmuje połączenia tylko z `localhost`. |

## Ustawienia i dane

| Decyzja | Dlaczego |
|---|---|
| **Moduły ustawień (`ISettingsModule`)**, UI rysuje pola z opisu modułu; zmiany bez restartu | Jedno miejsce na połączenia; nowy system = nowa klasa. |
| **Dane użytkownika w `%LOCALAPPDATA%\FlowHub`**: ustawienia, tokeny (DPAPI), katalog repo, mapy repo, znane modele | Poza repo — `git pull` ich nie nadpisuje, nic prywatnego nie trafi do commita. `repos.yaml` w repo to przykład startowy. |
| **Stan połączeń** (Atrapa / Działa / Nie działa / Nie ustawiono) pamiętany 5 minut; „Zapisz i sprawdź” | Widać, czy token się zapisał i czy połączenie działa. |
| **Pola z listą podpowiedzi z serwera** (środowiska, modele) | Zamiast wpisywać nazwy ręcznie. |

## Integracje

| Decyzja | Dlaczego |
|---|---|
| **Wybór implementacji czytany przy każdym użyciu** | Przełączenie Atrapa ↔ prawdziwa bez restartu. |
| **TFS: projekt i nazwa repo z adresu repo**; adres spoza skonfigurowanej kolekcji ignorowany | Repo bywają w różnych projektach; token idzie tylko do kolekcji z Ustawień. |
| **Branch `pull/N/merge` = PR nr N**; buildy PR doliczane do brancha (`RepoStatus:PullRequestBuildBranch`) | Tak TeamCity buduje pull requesty. |
| **Źródło buildów „Statusy z TFS”** obok TeamCity REST | Działa bez tokenu TeamCity i bez mapowania build configuration. |
| **TeamCity: filtr buildów w trzech wersjach**, od najpełniejszej do najprostszej | Serwer 2026.1 odrzucił `state:any` z `running:any` kodem 400. |
| **Octopus: na środowisku pokazujemy wersję wdrożoną teraz**; zielona = z tego brancha, szara = inna | Szybkie porównanie wersji. „Gotowe na DEV” liczy się tylko dla wersji z brancha. |
| **Release dopasowany do brancha** po build information, referencji Git, nazwie brancha w wersji albo wersji z numeru builda | Nie pokazujemy cudzego release jako „Twojego”. Dla brancha domyślnego — najnowszy. |
| **Komunikat błędu zawiera początek odpowiedzi serwera**; „Diagnostyka połączeń” pokazuje zapytania bez tokenów | Bez dostępu do serwerów to jedyny sposób ustalenia przyczyny. |
| **Dane z atrap są statyczne**, trzy scenariusze (zielony, w toku, czerwony) | Widać każdy stan w UI. |

## Repozytoria

| Decyzja | Dlaczego |
|---|---|
| **Lokalne kopie jako drzewo folderów**, skan do 2 poziomów, tylko odczyt Gita | Repo leżą luzem albo w folderach per branch. |
| **Skan i statusy pamiętane** (`Repositories:RefreshMinutes`, domyślnie 10) + „Odśwież teraz” | Strona nie odpytuje serwerów przy każdym wejściu. |
| **Szczegóły w zakładkach** (Pull request · Build · Wdrożenia · Procesy · Commity · Informacje), wspólne dla lokalnych kopii i zadania | Długie listy niczego nie rozpychają. Klik w komórkę otwiera właściwą zakładkę. |
| **Katalog repo edytowany w aplikacji**; zapis z formularza przepisuje plik | Ręczne komentarze w pliku przepadają. |
| **Procesy / usługi na maszynach**: CIM po WinRM, na Twoim koncie, sprawdzane na żądanie; nazwy walidowane i przekazywane jako argumenty | „Czy serwis wstał po wdrożeniu” bez agentów na maszynach. |
| **Diff w jednym widoku** (nie side-by-side); testy = status kroku testowego, bez liczby testów | KISS. Do porównywania obok siebie jest VS Code. |

## Interfejs

| Decyzja | Dlaczego |
|---|---|
| **Jedno miejsce na wygląd kontrolek** (domyślne ustawienia Vuetify, baza 15 px) | Spójnie i zwarto na każdej stronie. |
| **Kolejność kroków w edytorze strzałkami**, wykres trendu jako słupki CSS | KISS, mniej zależności. |

## Niezrobione

- **logFaces** — serwer ma REST API, ale brak jego opisu (jest tylko w podręczniku PDF). Potrzebny rozdział „REST API” albo przykład zapytania.
- Lista paczek do aktualizacji, pasek „ścieżka zmiany” (commit → PR → build → release), import katalogu repo jedną akcją, Copilot SDK jako runner.
