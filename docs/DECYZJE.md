# Decyzje podjęte samodzielnie (do przeglądu)

Lista rzeczy, które zdecydowałem sam podczas pracy autonomicznej. Każdą można cofnąć.

> **Jak czytać:** sekcje są w kolejności powstawania i opisują stan z danej fazy. Późniejsze fazy zmieniły część wcześniejszych ustaleń — m.in. Copilot CLI został sprawdzony na prawdziwym CLI (faza 9), przykładowe modele i ceny zastąpiło `auto` (faza 9), a „tylko atrapy” zastąpiły klienty TFS, Jira, TeamCity i Octopus (fazy 9 i 13). Aktualną listę rzeczy niesprawdzonych ma ostatnia sekcja.

## Faza 1

| Decyzja | Dlaczego |
|---|---|
| **Vuetify 4 zamiast PrimeVue** (+ ikony `@mdi/font`) | PrimeVue 5 jest komercyjne (baner „Invalid PrimeUI License”), a linia 4.x to już stara gałąź. Użytkownik poprosił o darmową bibliotekę z lepszym wsparciem. Vuetify: MIT, aktywnie rozwijane, największa społeczność, komplet komponentów. Ikony MDI są w paczce — działają bez internetu. |
| **.NET 10 (LTS), paczki Microsoft.* 10.0.x** | Wyszedł .NET 11 (STS), spec mówi o LTS. |
| **Microsoft.Testing.Platform** (`global.json`) | .NET 10 SDK nie wspiera już VSTest dla xUnit v3. Testy: `dotnet test --solution FlowHub.slnx`. |
| **`FlowHub.slnx`** zamiast `.sln` | Domyślny format solucji w .NET 10. |
| Skrypty `.ps1` w UTF-8 z BOM, składnia zgodna z Windows PowerShell 5.1 | Na maszynie nie ma `pwsh`; 5.1 bez BOM psuje polskie znaki. |

## Faza 2 — domena, YAML, kanban

| Decyzja | Dlaczego |
|---|---|
| **Repo wybierasz przy tworzeniu zadania** (przycisk „Zaproponuj” dopasowuje po słowach kluczowych, 0 tokenów) | Kroki „per repo” muszą znać listę repo, zanim flow ruszy. Osobny krok „rozpoznanie repo przez agenta” odpadł — Twój flow bazowy go nie ma. |
| **Katalog repo tylko w `repos.yaml`** (UI tylko do odczytu + „Wczytaj ponownie”) | KISS. Edycja w UI i „Importuj z TFS/TC/Octopus” wymagają prawdziwych połączeń. |
| **Flow w pamięci, bez cache w bazie** | Pliki YAML są źródłem prawdy; cache w bazie nic nie dawał. Niepoprawny plik nie blokuje pozostałych — błąd widać na Dashboardzie i w logu. |
| **Nieznany klucz w YAML = błąd** | Literówka typu `tokenBudgte` nie przejdzie po cichu. |
| **Typy zadań: Business, Bug, Technical, Spike** | Spec wymienia 6 (trzy warianty spike'a). Jeden `Spike` wystarcza na start; kolejny typ = nowy plik w `flows/`. |
| **Kolumny kanbana w `appsettings.json` → `Board:Columns`** | Konfigurowalne bez kompilacji. Kolumna to etap pracy ustawiany ręcznie, niezależny od kroku flow. |
| **Migracje EF Core** (`dotnet ef`, narzędzie lokalne w `dotnet-tools.json`) | Baza przeżywa zmiany schematu między fazami. Nowa migracja: `dotnet ef migrations add <Nazwa> --project src/FlowHub.Infrastructure --startup-project src/FlowHub.Infrastructure --output-dir Persistence/Migrations`. |
| **Repo demonstracyjne `demo://`** w `repos.yaml` | Bez dostępu do TFS skrypt klonowania tworzy lokalne repo-atrapę. W pracy podmieniasz adresy. |

## Faza 3 — silnik flow

| Decyzja | Dlaczego |
|---|---|
| **Flow rusza po kliknięciu „Uruchom flow”**, nie przy tworzeniu zadania | Nic nie dzieje się na dysku bez Twojej decyzji. |
| **Kroki z `requiresApproval: false` idą same** (folder, klon, branch, kopiowanie bloczków) | To proste skrypty bez AI. Każdy inny krok czeka na „Zatwierdź”. Zmienisz to jedną linią w YAML. |
| **„Akceptuj wynik” = zatwierdzenie następnego kroku**; „Powtórz z uwagą” działa na każdym zakończonym kroku | Osobny stan „wynik zaakceptowany” dublowałby klik. Powtórka wcześniejszego kroku wstrzymuje późniejsze. |
| **Kroki idą po kolei** (także „per repo”) | KISS i kontrola. Równoległość skryptów per repo — do dodania, gdy będzie potrzebna. Różne zadania działają równolegle. |
| **Skrypty dostają kontekst przez zmienne `FLOWHUB_*`** | Jeden prosty kontrakt; skrypt czyta tylko to, czego potrzebuje. Lista: `StepEnvironment.cs`. |
| **Po restarcie aplikacji** krok „w trakcie” dostaje status Błąd, a „zatwierdzony” wraca do akceptacji | Proces skryptu/agenta nie przeżywa restartu — decyzja wraca do Ciebie. |
| **Bloczki w repo zadania trafiają do `.git/info/exclude`**; śledzonych plików repo nie nadpisujemy | Agenci i skille FlowHub nie wejdą do PR ani nie zepsują własnych plików repo. |

## Faza 4 — agent Copilot

| Decyzja | Dlaczego |
|---|---|
| **Wybór agenta: `Agent:Runner` = `Fake` \| `CopilotCli`** (zamiast `Integrations:AgentRunner`) | Agent leży w warstwie Infrastructure, nie Integrations — osobna sekcja konfiguracji jest czytelniejsza. Reszta ustawień: `Agent:CliPath`, `Agent:FakeDelayMs`. |
| **Copilot przez CLI (`copilot -p … --output-format json`), bez SDK** | SDK nie potwierdza zdarzenia z tokenami, a CLI nie ma na tej maszynie. Jedna ścieżka do dopracowania w pracy zamiast dwóch niesprawdzonych. `IAgentRunner` pozwala dodać SDK później bez zmian w silniku. |
| **Zgoda w UI przed każdą sesją z `write` lub `shell`** (Fake i CLI) | Wymóg bezpieczeństwa. CLI w trybie `-p` nie zadaje pytań w trakcie, więc pytamy raz przed startem; narzędzia i tak są ograniczone do listy kroku (`--allow-tool`). Nigdy `--allow-all`. |
| **Sesja startuje w repo, katalog zadania przez `--add-dir`** | Copilot szuka agentów i skilli w `.github/` repo. Pliki wynikowe (`plan.md` itd.) lądują w katalogu zadania. |
| **Prompt = szablon z `blocks/prompts` + lista plików wejściowych i wynikowych** | Kontekst idzie plikami, nie rozmową — każdy krok to osobna, krótka sesja. Krok kończy się błędem, jeśli agent nie utworzył oczekiwanego pliku. |
| **„Rozmowa” z agentem = „Powtórz z uwagą”** (nowa sesja z dopisaną uwagą) | Interaktywny terminal (xterm + ConPTY) wymaga prawdziwego CLI do zbudowania i sprawdzenia — do zrobienia w pracy. |

**Nie sprawdzone — do zrobienia w pracy przy pierwszym uruchomieniu z `Agent:Runner = CopilotCli`:**
1. Nazwy zdarzeń i pól w JSONL (`CopilotJsonlParser.cs`) — przyjęte z dokumentacji SDK, nie z prawdziwego wyjścia CLI. Nieznane linie są pokazywane w konsoli, więc od razu będzie widać, co trzeba dopasować.
2. Czy CLI podaje zużycie tokenów w JSONL. Jeśli nie — plan B: `~/.copilot/session-state/<id>/events.jsonl`.
3. Pola frontmattera agentów (`tools`, `model`) — patrz `docs/copilot-cli.md`.
4. Jeśli `copilot` to skrypt `.cmd` (instalacja przez npm), ustaw w `Agent:CliPath` pełną ścieżkę do `copilot.exe` (instalacja przez winget).

## Faza 5 — tokeny i koszt

| Decyzja | Dlaczego |
|---|---|
| **Modele i ceny w `appsettings.json` → `Models` to PRZYKŁADY** (`gpt-5-mini`, `claude-sonnet-4.5`, `claude-opus-4.5`) | Pominęliśmy pytanie o model rozliczeń i listę modeli. **Uzupełnij `Tiers` i `Prices` tym, co ma Twoja firma** — inaczej koszt w raporcie jest orientacyjny. |
| **Koszt liczymy sami z cennika, gdy agent go nie poda** | Copilot rozlicza „premium requests”, nie dolary za token — koszt w USD to przelicznik do porównywania kroków, nie faktura. |
| **100% budżetu kroku = krok przerwany ze statusem Błąd** (podnosisz budżet i klikasz „Powtórz”) | Prostsze niż wstrzymywanie sesji w locie; decyzja i tak wraca do Ciebie. Przy 80% — ostrzeżenie w konsoli i żółty pasek. |
| **Budżet zadania: `Models:TaskTokenBudget`** (jeden dla wszystkich zadań, 0 = bez limitu) | KISS. Po wyczerpaniu kolejne kroki agenta nie startują. |
| **Sugestia modelu z `plan.md` nie ustawia się sama** — krok „Wybór modelu” to checklista, model zmieniasz w propozycji kroku | Automatyczne parsowanie planu byłoby kruche; Ty i tak zatwierdzasz. |
| **Wykres trendu jako proste słupki CSS** | Bez biblioteki wykresów — mniej zależności. |

## Fazy 6–7 — dashboard zadania i integracje

| Decyzja | Dlaczego |
|---|---|
| **Tylko implementacje Fake** dla PR (TFS), CI (TeamCity), wdrożeń (Octopus) i trackera (Jira). Prawdziwych klientów REST nie napisałem. | Bez dostępu do serwerów nie da się ich sprawdzić, a zasada brzmi „nie zgaduj API”. Porty są gotowe (`Application/Integrations/Ports.cs`); prawdziwa implementacja = nowa klasa + jeden wpis w `FlowHub.Integrations/DependencyInjection.cs` + nazwa w `appsettings.json` → `Integrations`. |
| **Mapowanie między systemami:** repo (`repos.yaml`) → build configuration TC + projekt Octopus, dalej po **nazwie brancha zadania** | Tak opisane w specyfikacji. Powiązanie po hashu commita dojdzie z prawdziwymi klientami. |
| **Trzy scenariusze danych Fake:** `Shared.Lib` zielony, `Billing.Api` w toku, `Customer.Portal` czerwony (konflikty, 2 nieudane testy) | W UI widać każdy stan komórki. Dane statyczne, jak ustaliliśmy. |
| **Brak `IDocsPublisher`** | Dokumentacja to Markdown w repo — szkic napisze krok agenta; osobny publikator nie jest potrzebny. |
| **Krok `Check: pull-request`** przed wiadomością na Teams; zapisuje `pr-<repo>.md` z linkiem | Wiadomość na Teams potrzebuje linku do PR. PR tworzysz sam (push tylko za Twoją zgodą) — krok go znajduje. Sprawdzenia `build` i `deployment` są dostępne, ale nie dodałem ich do flow. |
| **„Gotowe” = wdrożone na `RepoStatus:ReadyEnvironment`** (domyślnie DEV) | Napisałeś, że środowisko testowe to DEV. Zmienisz w konfiguracji. |
| **Diff w jednym widoku z kolorami** (nie side-by-side); nowe pliki nieśledzone na osobnej liście | KISS. Do porównywania obok siebie jest przycisk „VS Code”. |
| **Testy w tabeli = status ostatniego kroku testowego dla repo**, bez liczby testów | Liczba wymagałaby parsowania wyjścia `dotnet test` / `npm test` per technologia. |

**Nie zrobione z Fazy 6:** lista paczek do aktualizacji (`dotnet list package --outdated --vulnerable`) — wymaga sieci i prawdziwego repo .NET; najprościej dodać jako krok `Script`.
**Nie zrobione z Fazy 7:** „Importuj” katalog repo z TFS/TC/Octopus (wymaga prawdziwych połączeń), pasek „ścieżka zmiany” commit → PR → build → release.

## Faza 8 — edytor flow i retro

| Decyzja | Dlaczego |
|---|---|
| **Kolejność kroków strzałkami** (bez drag & drop) | KISS — spec dopuszczał oba warianty. |
| **Zapis z edytora nadpisuje plik YAML bez komentarzy** | Serializator nie zachowuje komentarzy. Pola i kolejność zostają; historię masz w Git. |
| **Retro dostaje statystyki zadania w prompcie** (`{{stats}}`: tokeny, koszt, próby, błędy, Twoje uwagi) | Fakty zamiast zgadywania; agent nie ma dostępu do bazy. |
| **Propozycje z retro wdrażasz ręcznie w edytorze bloczków** (zakładka „Bloczki”), commit robisz sam | Automatyczne nakładanie diffów wygenerowanych przez model i auto-commit są ryzykowne; zasada mówi „zawsze szkic do akceptacji”. |
| **Edytor bloczków zapisuje tylko `.md` i `.ps1` w `blocks/`** (skrypty z BOM) | Bezpieczeństwo ścieżek + pułapka PowerShell 5.1. |

**Nie sprawdzone:** przeciąganie kart prawdziwą myszą (podgląd nie emuluje natywnego drag & drop). Logika upuszczenia i zapis w API są sprawdzone zdarzeniami.

## Faza 9 — ustawienia, prawdziwy Copilot CLI, klienty TFS i Jira (2026-11-11)

| Decyzja | Dlaczego |
|---|---|
| **Strona „Ustawienia” z modułów** (`ISettingsModule`): Modele i plan, Copilot CLI, TFS, Jira, TeamCity, Octopus. UI rysuje pola z opisu modułu. | Prośba o jedno miejsce na połączenia, modularne. Nowy system = nowa klasa + linia w DI. |
| **Ustawienia w `%LOCALAPPDATA%\FlowHub\settings.json`**, nakładane na `appsettings.json`; działają od razu, bez restartu | Poza repo, więc nic prywatnego nie trafi do commita. Katalog zmienisz przez `Settings:Directory`. |
| **Tokeny w `secrets.json` obok, zaszyfrowane DPAPI** (bieżący użytkownik Windows). API nigdy ich nie zwraca. Zmienne środowiskowe / user-secrets nadal działają jako zapas (np. `Tfs__Token`). | Zasada „żadnych sekretów w repo i logach”, a wpisywanie tokenu w UI jest wygodniejsze niż user-secrets. |
| **Plan „bez limitu” (`Models:Plan = Unlimited`)**: budżety nie ostrzegają i nie zatrzymują, koszt nie jest liczony | Copilot Enterprise bez ograniczeń w pracy. Tokeny dalej są liczone do raportu. |
| **Poziomy modeli domyślnie `auto`, bez przykładowych cen** | Na prawdziwym koncie były dostępne inne modele niż przykładowe z Fazy 5 — zmyślone id wywaliłyby krok. Id modeli wpisujesz w Ustawieniach. |
| **Agent może zapisać tylko zadeklarowane pliki wynikowe kroku** (`--allow-tool write(<pełna ścieżka>)`) | W trybie `-p` CLI odrzuca narzędzia spoza listy; bez tego `plan.md` czy `context-*.md` by nie powstały. Pełny `write` dalej tylko z listy kroku i za zgodą. |
| **Wszyscy agenci dostali narzędzie `edit`** we frontmatterze | Lista `tools` agenta ogranicza narzędzia; bez `edit` agent nie umiał zapisać pliku wynikowego (sprawdzone na prawdziwym CLI). |
| **Tokeny z pliku `--usage-output-file`** po zakończeniu sesji; przekroczony budżet kroku daje wtedy ostrzeżenie, nie błąd | CLI nie podaje tokenów w strumieniu. Skoro sesja już się skończyła, przerywanie nic by nie dało. Do budżetu liczymy wejście bez odczytów z cache. |
| **Klient TFS** (pull requesty, wątki, polityki, lista repo) i **klient Jira** (treść zadania) | Żeby Ustawienia miały co przełączać. Mapowanie JSON jest pokryte testami na przykładach z dokumentacji. |

**Sprawdzone na prawdziwym Copilot CLI 1.0.109:** format JSONL, wczytanie agenta i skilli z `.github/`, zgoda w UI, zapis pliku wynikowego, tokeny — cały krok „Analiza technologiczna” w aplikacji.

**Nie sprawdzone:**
1. Klienty TFS i Jira nie widziały prawdziwego serwera — adresy i pola są wg dokumentacji REST. Po wpisaniu adresu i tokenu użyj „Sprawdź połączenie”.
2. TeamCity i Octopus mają tylko kartę ustawień z testem połączenia; dane dalej z atrapy.
3. Szyfrowanie DPAPI działa tylko na Windows (aplikacja jest windowsowa).
4. Koszt w kredytach AI z CLI (`totalNanoAiu`) nie jest przeliczany na pieniądze.

## Faza 10 — terminal Copilota w aplikacji (2026-11-11)

| Decyzja | Dlaczego |
|---|---|
| **Prawdziwy terminal: xterm.js ↔ WebSocket ↔ pseudokonsola Windows (ConPTY)** z interaktywnym `copilot` | Prośba o terminal w aplikacji. Pełny interfejs CLI (pytania o zgodę, `/model`, `/usage`) działa bez pisania własnej nakładki. |
| **ConPTY przez własne wywołania Win32** (`PseudoConsoleProcess`), bez paczki NuGet | Kilka funkcji systemowych; paczki do PTY dla .NET są słabo utrzymane. Tylko Windows. |
| **Klient nie podaje polecenia ani ścieżki** — tylko id zadania, repo albo kroku; serwer uruchamia wyłącznie skonfigurowane Copilot CLI w katalogu zadania/repo | Terminal w przeglądarce to zdalne wykonywanie poleceń — zawężamy do minimum. |
| **WebSocket terminala przyjmuje tylko strony z tego komputera** (nagłówek Origin = localhost) | Inaczej dowolna strona otwarta w przeglądarce mogłaby podłączyć się do terminala. |
| **„Kontynuuj w terminalu” przy kroku agenta** (`copilot --resume=<id sesji kroku>`) | Id sesji zapisujemy przy kroku (nowa kolumna `SessionId`, migracja `StepSession`). Działa po krokach wykonanych prawdziwym CLI. |
| **Zgody w terminalu daje sam Copilot** (pytanie w oknie), bez `--allow-all` | Zgodnie z zasadami bezpieczeństwa. |

**Sprawdzone:** start interaktywnego Copilota w repo zadania, pytanie o zaufanie do folderu, pytanie i odpowiedź modelu, zamknięcie okna kończy proces.
**Nie sprawdzone:** „Kontynuuj w terminalu” na prawdziwej sesji kroku (mechanizm `--resume` jest w dokumentacji CLI), dłuższa praca z edycją plików w terminalu.

## Faza 11 — edycja plików, krok „co dalej?”, generator bloczków (2026-11-11)

| Decyzja | Dlaczego |
|---|---|
| **Pliki `.md` zadania edytujesz w aplikacji** (przycisk „Edytuj” w podglądzie pliku) | Kolejne kroki agenta czytają te pliki z dysku, więc poprawka np. w `plan.md` działa od następnego kroku. Tylko `.md` bezpośrednio w katalogu zadania. |
| **Nowy krok ręczny „Przegląd realizacji — co dalej?”** zaraz po realizacji (flow Biznesowe, Błąd, Techniczne) | Prośba: po realizacji może „nie być kolorowo”. Wpisujesz polecenie, agent realizacji rusza ponownie, a przegląd wraca po wyniku — w pętli, aż potwierdzisz. |
| **Mechanizm jest ogólny: `followUp: <id kroku agenta>` na kroku Manual** | Da się go użyć po dowolnym kroku agenta (edytor flow ma na to pole), bez zmian w silniku. |
| **Polecenie „co dalej” uruchamia agenta bez drugiego zatwierdzania**; kroki między nim a przeglądem (np. testy) idą jeszcze raz | Sam to przed chwilą zleciłeś. Zgoda na `write`/`shell` w UI dalej obowiązuje. |
| **Powtórka kroku z uwagą kontynuuje tę samą sesję Copilota** (`--resume=<id>`) | Agent pamięta, co zrobił, więc poprawka jest tańsza i trafniejsza. Sprawdzone na prawdziwym CLI. |
| **Generator bloczków** (Flow i bloczki → Bloczki → „Generuj nowy bloczek”): opis oczekiwań + wzorzec tego samego rodzaju → szkic w edytorze | Prosty: jedno pole opisu. Agent nie dostaje narzędzi i pracuje w pustym katalogu. Zapis dopiero po Twoim „Zapisz” (zasada: zmiany w `blocks/` zawsze jako szkic). |
| **Na atrapie generator zwraca oznaczoną kopię wzorca** | Żeby obieg dało się obejrzeć bez Copilota; treść pisze model dopiero po włączeniu Copilot CLI. |

**Sprawdzone:** pętla „co dalej” w aplikacji (na atrapie agenta), edycja `plan.md` w UI, generator skilla prawdziwym Copilotem (17 s), `--resume` w trybie `-p`.
**Nie sprawdzone:** pętla „co dalej” z prawdziwym agentem realizacji zmieniającym kod. Tokeny zużyte przez generator nie trafiają do raportu (nie należą do żadnego zadania).

## Faza 12 — lokalne repozytoria i wybór repo z TFS (2026-11-11)

| Decyzja | Dlaczego |
|---|---|
| **Folder z repozytoriami w Ustawieniach** (`Repositories:ScanRoot`, np. `C:\tfs`); strona Repozytoria pokazuje znalezione kopie | Prośba: repo leżą w folderach per branch albo luzem. Szukamy do 2 poziomów w głąb (folder → repo albo folder → folder brancha → repo). |
| **Dla każdej kopii: branch, ostatnie commity, niezacommitowane pliki, a dla repo z katalogu — PR, build, release i środowiska tego brancha** | „Widoczność PR, commitów, TC, OC” w jednym miejscu. Dopasowanie do `repos.yaml` po adresie origin, potem po nazwie. |
| **Tylko odczyt Gita** (`config`, `rev-parse`, `log`, `status`) | FlowHub niczego nie zmienia w Twoich kopiach. |
| **„Dodaj do katalogu” przy lokalnym repo i „Dodaj repo z TFS” w nowym zadaniu** dopisują wpis na końcu `repos.yaml` | Komentarze i ręczne formatowanie pliku zostają. Adres bierzemy z origin / z serwera, nie z przeglądarki. `ciBuildConfigurations`, `deployProject` i `keywords` uzupełniasz potem ręcznie. |
| **Lista repo z TFS ma atrapę** (repo demo + trzy spoza katalogu) i prawdziwą implementację (Ustawienia → TFS → „Lista repozytoriów do wyboru”) | Żeby wybór dało się obejrzeć bez serwera. |
| **Wspólny `BranchStatusService`** dla tabeli repo w zadaniu i lokalnych kopii | Jedno miejsce odpytywania TFS / TeamCity / Octopus. |
| **[docs/polaczenia-w-pracy.md](polaczenia-w-pracy.md)** — jak podłączyć serwery | Skrypt zbierający próbki API został usunięty w porządkach: zastąpiła go „Diagnostyka połączeń” w aplikacji (faza 13). |

**Sprawdzone:** skan na katalogu `work/` (8 kopii w folderach zadań), statusy z atrap, dodanie repo z listy TFS-atrapy w oknie nowego zadania.
**Nie sprawdzone:** skan prawdziwego `C:\tfs` (duże repo, worktree), lista repo z prawdziwego TFS, skrypt zbierający próbki na prawdziwych serwerach (sprawdzona tylko składnia).

## Faza 13 — uwagi po pierwszym użyciu w pracy (2026-11-12)

Wersje u użytkownika: Azure DevOps Server 25H2 (patch 9), TeamCity 2026.1, Octopus 2026.2.

| Decyzja | Dlaczego |
|---|---|
| **TFS: projekt i nazwa repo brane z adresu repo** (`…/Kolekcja/Projekt/_git/Repo`), nie z jednego projektu w Ustawieniach | Najbardziej prawdopodobny powód „nie widzi PR”: repo w innym projekcie kolekcji albo inna nazwa niż w katalogu. Adres spoza skonfigurowanej kolekcji jest ignorowany — token idzie tylko do niej. |
| **PR: zapasowe wyszukiwanie w całym projekcie; branch `pull/N/merge` = PR nr N** | Gdy serwer nie zna repo po nazwie. Na zrzucie z TeamCity build PR idzie z brancha `pull/36816/merge`. |
| **Nowe źródło buildów „Statusy z TFS”** — statusy przy commicie i pull requeście (to, co TFS pokazuje w zakładce Status / Pipelines) | Sugestia użytkownika. Działa bez tokenu TeamCity i bez mapowania build configuration; link prowadzi do builda w TeamCity. |
| **Klient TeamCity** (buildy brancha także trwające, tekst statusu z licznikami testów, nieudane testy, zmiany) i **klient Octopus** (`progression`: środowiska × release × stan) | Prośba o prawdziwe źródła. Adresy i pola sprawdzone w dokumentacji (TeamCity REST, dokumentacja Octopus i kod oficjalnego klienta). |
| **Octopus: release dopasowany do brancha** po build information, referencji Git, nazwie brancha w wersji albo wersji z numeru builda (build „1.30.0 #643” → release 1.30.0). Bez dopasowania środowiska są puste; dla brancha domyślnego bierzemy najnowszy release. | Nie pokazujemy cudzego release jako „Twojego”. Regułę trzeba potwierdzić na prawdziwych danych. |
| **Buildy PR doliczane do brancha** (`RepoStatus:PullRequestBuildBranch`, domyślnie `pull/{id}/merge`) | Inaczej build z Twoimi zmianami byłby niewidoczny przy branchu zadania. |
| **Środowiska: lista pokazywanych i środowisko domyślne w Ustawieniach** (karta Octopus) | „Nie mam UAT” i „wybór domyślnego DEV”. „Sprawdź połączenie” wypisuje nazwy środowisk z serwera. |
| **Skróty TFS / PR / TC / Octopus przy każdym repo** | Adresy składane z adresu repo i adresów serwerów z Ustawień — działają też na atrapie danych. |
| **Diagnostyka połączeń** przy repo: lista zapytań (bez tokenów) i odpowiedzi serwerów, z przyciskiem „Kopiuj wynik” | Bez dostępu do serwerów to jedyny sposób, żeby ustalić, czemu czegoś nie widać. |
| **Ustawienia: lista modułów po lewej, jeden formularz po prawej**; stan (Atrapa / Działa / Nie działa / Nie ustawiono), „Token zapisany / Brak tokenu”, „Zapisz i sprawdź” | Układ kart „rozjeżdżał się”, nie było widać, czy token się zapisał i czy połączenie działa. |
| **Stan integracji w pasku bocznym**, odświeżany najwyżej co 5 minut | Widać z każdej strony; kliknięcie prowadzi do Ustawień. |
| **Repozytoria: zakładka „Moje repozytoria” jako drzewo folderów** (pierwsza), większe wiersze ze statusami | „Mało widoczne” i „jak drzewo katalogów”. |
| **Skan i statusy pamiętane** (`Repositories:RefreshMinutes`, domyślnie 10 min; także w pamięci strony) | „Za każdym razem się ładuje”. Przycisk „Odśwież teraz” czyta od razu. |
| **Katalog repo edytowalny w aplikacji** (dodaj / edytuj / usuń, podpowiedzi build configuration i projektów z serwerów) + wyjaśnienie, po co jest | „Nie można edytować repos.yaml” i „nie wiem po co repos.yaml”. Zapis z formularza przepisuje plik (z nagłówkiem), ręczne komentarze przepadają. |
| **Mapa repo** (`remember: true` na kroku analizy): wynik zapisywany w `knowledge/<repo>.md` z commitem; kolejne zadanie dostaje ją w pliku wynikowym i polecenie „zaktualizuj od commita X” | Żeby nie robić analizy od zera — mniej tokenów. Katalog `knowledge/` jest poza gitem. Mapę można obejrzeć, poprawić i usunąć (Repozytoria → Katalog → Mapa). |

**Nie sprawdzone (brak dostępu do serwerów):** klienty TeamCity i Octopus oraz statusy z TFS — tylko testy mapowania na przykładach z dokumentacji; adresy stron w przeglądarce (parametr `branch` w TeamCity, ścieżki portalu Octopus, `?version=GB…` w TFS); reguła dopasowania release do brancha; aktualizacja mapy repo prawdziwym agentem (sprawdzone na podstawionym).
**Jeśli coś nie działa:** otwórz repo na stronie Repozytoria → „Diagnostyka połączeń” → „Kopiuj wynik”.

### Poprawka po teście na prawdziwym TeamCity (2026-11-12)

- **Sprawdzone na prawdziwych serwerach:** lista build configuration z TeamCity i lista projektów z Octopus (podpowiedzi w edycji repo) — adresy i tokeny działają.
- **TeamCity 2026.1 odrzucił kodem 400 filtr buildów** (`state:any` razem z `running:any`). Filtr nie zawiera już `running`, a przy kolejnym 400 FlowHub próbuje dwóch prostszych wersji.
- **Komunikat błędu zawiera teraz początek odpowiedzi serwera** (bez HTML, do 400 znaków) — TeamCity pisze w niej, który fragment zapytania jest zły. Widać to w „Diagnostyce połączeń”.
- Nadal niesprawdzone: sam odczyt buildów po poprawce, testy i zmiany z TeamCity, wdrożenia z Octopus.

## Faza 14 — widok repo w zakładkach, dane poza repozytorium, spójny interfejs (2026-11-12)

Potwierdzone na serwerach użytkownika (zrzut ekranu): pull request z TFS, build z TeamCity z listą nieudanych testów, środowiska z Octopus.

| Decyzja | Dlaczego |
|---|---|
| **Szczegóły repo w zakładkach** (Pull request · Build · Wdrożenia · Commity · Informacje), stała wysokość z przewijaniem | Długa lista testów „rozjeżdżała” widok. Ten sam komponent w lokalnych kopiach i w tabeli repo zadania. |
| **Zwarty wiersz: PR, build z numerem, wersje na środowiskach**; kolumna „Release” usunięta (jest w zakładce Wdrożenia) | Prośba użytkownika. Klik w PR / build / środowisko rozwija szczegóły od razu na właściwej zakładce. |
| **Na środowisku pokazujemy wersję wdrożoną teraz** (dowolny release); zielona = wersja z tego brancha, szara = inna | Żeby szybko porównać wersje między środowiskami. „Gotowe na DEV” liczy się dalej tylko dla wersji z brancha. |
| **Katalog repo i mapy repo w folderze ustawień** (`%LOCALAPPDATA%\FlowHub`), pierwsze uruchomienie kopiuje dotychczasowy `repos.yaml` | Żeby `git pull` aplikacji nie nadpisywał katalogu i nie trzeba go było robić od nowa. Plik w repozytorium zostaje jako przykład startowy. |
| **Karta „Gdzie są zapisane dane”** w Ustawieniach | Prośba użytkownika — pełne ścieżki do ustawień, tokenów, katalogu, map, bazy i zadań. |
| **Środowiska wybierane z listy z serwera wdrożeń** (pojedyncze i wielokrotne pole z podpowiedziami) | Zamiast wpisywać nazwy ręcznie. Mechanizm ogólny: pole ustawień z `Suggest`. |
| **Modele: lista podpowiedzi z modeli widzianych w sesjach CLI** (+ `auto`) | Copilot CLI nie ma polecenia wypisującego modele poza sesją (sprawdzone: `--model` z nieistniejącą nazwą nie podaje listy). Planu / limitów konta CLI też nie udostępnia. |
| **„Klonuj” w katalogu repo** → `{folder}\{nazwa repo}`, bez okna logowania Gita | Prośba użytkownika. Sprawdzone prawdziwym `git clone` na lokalnym repo. |
| **Jedno miejsce na wygląd kontrolek** (domyślne ustawienia Vuetify: zwarte pola, tabele, listy, zakładki; baza 15 px) | Spójny, zwarty interfejs na wszystkich stronach bez poprawiania każdej osobno. |

**Nie sprawdzone:** klonowanie z prawdziwego TFS (zależy od danych logowania zapisanych w Gicie), lista środowisk z prawdziwego Octopus w polu wyboru, lista modeli po sesjach na koncie firmowym.

**Pytania użytkownika bez wdrożenia (do decyzji):**
- **logFaces** — serwer ma REST API (zapytania o logi, JSON), ale adresy i parametry są tylko w podręczniku / na serwerze; nie udało się ich potwierdzić z publicznej strony. Do wdrożenia potrzebny jest przykład zapytania z serwera użytkownika.
- **Proces / usługa na zdalnej maszynie po wdrożeniu** — wykonalne z PowerShell (`Get-CimInstance Win32_Process -ComputerName …` albo `Get-Service -ComputerName …`), wymaga uprawnień do maszyny i włączonego WinRM; nazwy procesów przypisywane do repo w katalogu.

## Faza 15 — procesy i usługi na maszynach (2026-11-12)

| Decyzja | Dlaczego |
|---|---|
| **Przy repo w katalogu lista „Procesy / usługi na maszynach”** (maszyna, początek nazwy, etykieta np. środowisko) | Prośba: po wdrożeniu sprawdzić, czy serwis wstał; nazwa w Menedżerze zadań różni się od nazwy repo, więc przypisuje się ją ręcznie. |
| **Zakładka „Procesy” w szczegółach repo**, sprawdzanie dopiero po wejściu na zakładkę i na „Sprawdź teraz” | To zapytanie do zdalnych maszyn — bywa wolne, więc nie robimy go przy każdym odświeżeniu listy. |
| **Odczyt przez CIM (`Get-CimInstance Win32_Process` / `Win32_Service`) po WinRM**, na koncie, na którym działa FlowHub | Wbudowane w Windows, bez agentów na maszynach. Tylko odczyt. Szukamy i procesu, i usługi; zatrzymana usługa = „nie działa”. |
| **Nazwy maszyny i procesu przechodzą walidację i są argumentami skryptu**, nie fragmentem jego treści | Te wartości trafiają do polecenia systemowego. |
| **Błąd jednej maszyny nie przerywa sprawdzania pozostałych**; komunikat to jedna linia z podpowiedzią o WinRM | Czytelność. |

**Sprawdzone:** proces na tym komputerze, nieosiągalna maszyna, odrzucanie niebezpiecznych nazw.
**Nie sprawdzone:** prawdziwa zdalna maszyna (WinRM, uprawnienia konta, zapora).
**logFaces — nadal otwarte:** serwer ma REST API, ale jego opis jest tylko w podręczniku PDF, którego tekstu nie dało się odczytać; użytkownik korzysta z klienta desktopowego. Potrzebny rozdział „REST API” z podręcznika albo przykład działającego zapytania.
