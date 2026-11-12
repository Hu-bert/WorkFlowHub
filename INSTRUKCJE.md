# FlowHub — centrum przepływu pracy z agentami GitHub Copilot

Webowe „CRM zadań programistycznych”: tworzę zadanie danego **typu**, aplikacja prowadzi je przez **flow** z **bloczków** (skrypty, agenci Copilot, kroki ręczne, sprawdzenia), pokazuje na żywo pracę Copilot CLI, liczy tokeny i na końcu robi **retro**. Wszystko **za moją akceptacją na każdym etapie**.

Aplikacja lokalna, jednoosobowa (bez logowania), Windows + PowerShell. Jeden proces: API serwuje zbudowany frontend.

- Decyzje i to, czego nie sprawdzono: [docs/DECYZJE.md](docs/DECYZJE.md)
- Podłączanie serwerów w pracy: [docs/polaczenia-w-pracy.md](docs/polaczenia-w-pracy.md)
- Ściągawka Copilot CLI: [docs/copilot-cli.md](docs/copilot-cli.md)

## 1. Zasady pracy

1. **Praca autonomiczna**: zmiana na osobnym branchu → zielony `./check.ps1` → merge `--no-ff` do `main` (lokalny PR). **`git push` tylko po wyraźnej zgodzie.**
2. Decyzje podjęte samodzielnie i rzeczy niesprawdzone zapisuj w [docs/DECYZJE.md](docs/DECYZJE.md).
3. **Nie zgaduj API.** Przed użyciem zewnętrznego API (Copilot CLI, TFS, TeamCity, Octopus, Jira…) sprawdź dokumentację; czego nie da się sprawdzić na serwerze — oznacz jako niesprawdzone.
4. Lepsze rozwiązanie niż zamówione → zaproponuj; przy pracy autonomicznej wdrażaj tylko to, o co poproszono.
5. Rozmowa i teksty w aplikacji po polsku. Pisz krótko.
6. Kod czysty, modularny, KISS; nowe rzeczy dodawane przez interfejs + DI, bez ruszania rdzenia.

## 2. Bezpieczeństwo (nie negocjujemy)

- Żadnych sekretów w kodzie, YAML, logach ani promptach. Tokeny tylko w zaszyfrowanym pliku w folderze ustawień (albo user-secrets / zmienne środowiskowe).
- Agent nigdy nie dostaje `--allow-all` / `--yolo`. `shell` i `write` tylko z listy kroku i za zgodą w UI; pliki wynikowe kroku — tylko zadeklarowane ścieżki.
- Agent pracuje tylko w katalogu zadania.
- Dokumentacja i zmiany w `blocks/` — zawsze szkic do akceptacji.
- Systemy firmowe: **tylko odczyt** (same zapytania GET, odczyt procesów). FlowHub niczego tam nie zmienia.
- Żadnego `git push` bez zgody.

## 3. Stack

**Backend:** .NET 10 (LTS), C#, ASP.NET Core minimal API + SignalR, EF Core + SQLite (migracje), Serilog, YamlDotNet. `Directory.Packages.props`, `nullable enable`, `TreatWarningsAsErrors`. Testy: xUnit v3 + NSubstitute (`dotnet test --solution FlowHub.slnx`).

**Frontend:** Vue 3 (`<script setup>`) + TypeScript strict + Vite, Pinia, Vue Router, **Vuetify 4** + `@mdi/font`, xterm.js, `@microsoft/signalr`. Testy: Vitest + Vue Test Utils. ESLint + Prettier.

```
src/FlowHub.Domain          encje i reguły (zero zależności)
src/FlowHub.Application     use case'y, silnik flow, porty
src/FlowHub.Infrastructure  EF Core, YAML, PowerShell, Git, Copilot CLI, terminal (ConPTY), ustawienia i sekrety
src/FlowHub.Integrations    TFS, TeamCity, Octopus, Jira (+ atrapy)
src/FlowHub.Api             endpointy, SignalR, hosting frontendu
web/                        Vue 3 + TypeScript
flows/                      typy zadań i ich kroki (YAML)
blocks/                     agenci, skille, prompty, skrypty, szablony (po polsku, uniwersalne)
tests/                      testy backendu (frontend: web/src/**/__tests__)
```

Kierunek zależności: `Api → Application → Domain`, `Infrastructure / Integrations → Application`.

## 4. Jak to działa

**Zadanie** (`WorkItem`) ma typ (= flow z YAML), repozytoria (`TaskRepo`, 1..N, ten sam branch) i kroki (`StepRun`). Jedno zadanie = jeden katalog `work/<KLUCZ>-<slug>/`; każdy krok agenta = osobna sesja Copilot, kontekst przekazywany plikami (`context-<repo>.md`, `plan.md`, `summary-<repo>.md`).

**Rodzaje kroków** (`StepKind`): `Script` (PowerShell, bez AI), `Agent` (Copilot CLI z agentem i promptem), `Manual` (checklista), `Check` (stan z zewnątrz: `pull-request`, `build`, `deployment`).

**Stany kroku:** `Pending → Proposed → Approved → Running → Succeeded | Failed | Skipped`. Przejść pilnuje Domain. Każdy krok: propozycja (prompt do edycji, model, budżet) → Zatwierdź / Pomiń → konsola na żywo → wynik → dalej albo „Powtórz z uwagą” (ta sama sesja Copilota).

**Flow bazowy:** folder zadania → klon repo → branch → kopiowanie agentów i skilli → analiza technologiczna (`remember: true` — zapisuje **mapę repo**, aktualizowaną w kolejnych zadaniach) → plan → wybór modelu → realizacja → **przegląd „co dalej?”** (`followUp: implement` — polecenie wraca do agenta, w pętli) → testy → opis dla architektów → mail (szkic) → sprawdzenie PR → Teams (szkic) → retro.

**Pola kroku w YAML:** `id`, `name`, `kind`, `script` / `agent` + `prompt` / `check` / `checklist`, `model` (`tier:cheap|standard|premium` albo id), `tokenBudget`, `requiresApproval`, `perRepo`, `allowedTools`, `inputs`, `outputs`, `followUp`, `remember`.

**Tokeny:** routing `tier:*` → model (domyślnie `auto`), budżet kroku i zadania (80% ostrzeżenie, 100% stop), raport per model / typ / krok / zadanie. Plan „bez limitu” (Copilot Enterprise) wyłącza budżety i wycenę.

## 5. Co jest w aplikacji

| Strona | Co robi |
|---|---|
| Dashboard, Kanban | zadania, kolumny wg etapu pracy (konfigurowalne), kropki statusu per repo |
| Zadanie | kroki flow, konsola agenta, zgody na narzędzia, tabela repo, pliki `.md` (edytowalne), diff, **terminal Copilota** (xterm.js + ConPTY, także kontynuacja sesji kroku) |
| Repozytoria | **moje kopie na dysku** jako drzewo folderów (PR, build, wersje na środowiskach, szczegóły w zakładkach, procesy na maszynach, diagnostyka połączeń) oraz **katalog repo** (edycja, mapa repo, klonowanie) |
| Flow i bloczki | edytor flow (zapis do YAML), edytor i **generator bloczków** (szkic na wzór istniejącego) |
| Tokeny | raport zużycia i routing modeli |
| Ustawienia | moduły połączeń (Copilot CLI, modele i plan, repozytoria lokalne, TFS, buildy, Octopus i środowiska, Jira), stan połączeń, miejsce zapisu danych |

**Integracje** (każda za portem, wybór w Ustawieniach, zmiana bez restartu):

| Port | Implementacje |
|---|---|
| `ITaskSource` | Atrapa, Jira REST |
| `IPullRequestService`, `IRepositoryDirectory` | Atrapa, TFS / Azure DevOps Server REST |
| `ICiServer` | Atrapa, statusy buildów z TFS, TeamCity REST |
| `IDeployServer` | Atrapa, Octopus REST |
| `IAgentRunner` | Atrapa, Copilot CLI (`copilot -p … --output-format json`) |
| `IRemoteProcessProbe` | procesy i usługi Windows przez CIM / WinRM |

**Dane użytkownika** leżą poza repozytorium, w `%LOCALAPPDATA%\FlowHub`: `settings.json`, `secrets.json` (DPAPI), `repos.yaml` (katalog repo), `knowledge/` (mapy repo), `models.json`. Plik `repos.yaml` w repo to tylko przykład startowy. Baza (`flowhub.db`) i `work/` są poza gitem.

## 6. Jak rozszerzać

| Chcę dodać… | Robię… |
|---|---|
| typ zadania | plik w `flows/` albo „Nowy typ zadania” w UI |
| agenta / skill / prompt / skrypt | plik w `blocks/`, zakładka „Bloczki” albo generator |
| rodzaj kroku | klasa `IStepExecutor` + linia w `Application/DependencyInjection.cs` |
| integrację | implementacja portu + wpis w `Integrations/DependencyInjection.cs` + karta ustawień (`ISettingsModule`) |
| ustawienie | pole w module ustawień (`SettingField`; `Suggest` = lista z serwera). **Nie** nowy wpis do ręcznej edycji w `appsettings.json` |
| stronę w UI | widok w `web/src/views` + trasa w `router/index.ts` + pozycja w `layout/navigation.ts` |

**Wygląd:** ciemny motyw fioletowy; gęstość i warianty kontrolek ustawia się w jednym miejscu (`web/src/plugins/vuetify.ts`, `web/src/styles/main.css`) — widoki ich nie nadpisują. Status zawsze jako ikona + kolor + tekst.

## 7. Środowisko użytkownika

- Praca: Azure DevOps Server 25H2 (Git), TeamCity 2026.1, Octopus 2026.2, Jira (nowa z API; stara — treść wklejana ręcznie), GitHub Copilot Enterprise.
- Branch zadania np. `ZR-3925-2`; gałąź główna `master`; build PR w TeamCity idzie z brancha `pull/<nr>/merge`; numer builda to „wersja #licznik”.
- Kopie repo w `C:\tfs` (luzem albo w folderach per branch).
- Pułapki PowerShell 5.1: skrypty `.ps1` muszą mieć BOM; nie ma `||`; komunikaty commitów przez `git commit -F`.

## 8. Otwarte tematy

- **logFaces** (logi po domenie i hoście, serwery `v-logfaces:8050` / `p-logfaces`): serwer ma REST API, ale brak jego opisu — potrzebny rozdział „REST API” z podręcznika albo przykład zapytania.
- Lista paczek do aktualizacji (`dotnet list package --outdated --vulnerable`), pasek „ścieżka zmiany” commit → PR → build → release, Copilot SDK jako drugi runner — niezrobione.
