# Decyzje podjęte samodzielnie (do przeglądu)

Lista rzeczy, które zdecydowałem sam podczas pracy autonomicznej. Każdą można cofnąć.

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
