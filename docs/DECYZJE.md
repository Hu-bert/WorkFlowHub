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

**Nie sprawdzone:** przeciąganie kart prawdziwą myszą (podgląd nie emuluje natywnego drag & drop). Logika upuszczenia i zapis w API są sprawdzone zdarzeniami.
