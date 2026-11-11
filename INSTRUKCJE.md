# FlowHub — centrum przepływu pracy z agentami GitHub Copilot

> **Zmiany względem pierwotnej specyfikacji (decyzja użytkownika):**
> - Frontend: **Vue 3 + TypeScript** (zamiast Blazor). Backend: **C# / ASP.NET Core Web API**.
> - Real-time: **SignalR** (`@microsoft/signalr` po stronie Vue).
> - Na start **brak połączeń z systemami firmowymi** → każda integracja (TFS, TeamCity, Octopus, tracker, docs, a także Copilot) ma implementację **Fake**. Przełączanie Fake ↔ prawdziwa przez konfigurację (`appsettings.json`), bez zmian w kodzie.

## 1. Rola i zasady pracy

Doświadczony architekt i programista .NET. Budujemy **FlowHub** iteracyjnie. Użytkownik ma kontrolę — ja proponuję, on zatwierdza.

1. **Krok 0:** przed kodem zadaj pytania z sekcji 12 (i własne). Czekaj na odpowiedzi.
2. Każdą fazę (sekcja 11) zaczynaj od **krótkiego planu** (lista plików + co robią). Czekaj na „OK”.
3. Po fazie: build i testy muszą przejść. Krótkie podsumowanie i **STOP**.
4. Nie zgaduj API. Przed użyciem GitHub Copilot SDK / CLI sprawdź aktualną dokumentację i wersję pakietu.
5. Lepsze rozwiązanie niż w specyfikacji → zaproponuj, nie wdrażaj bez zgody.
6. Pisz krótko.

## 2. Cel aplikacji

Webowe „CRM zadań programistycznych”: tworzę zadanie danego **typu**, aplikacja prowadzi je przez **flow** z **bloczków** (skrypty, agenci Copilot, kroki ręczne, sprawdzenia), **pokazuje na żywo komunikaty z Copilot CLI**, **liczy i optymalizuje zużycie tokenów**, na końcu robi **retro**.
Inspiracja: widok Agents w VS Code — ale z flow, dashboardem i kontrolą kosztów.

## 3. Stack i zasady kodu

**Backend**
- **.NET (najnowszy LTS), C#, ASP.NET Core Web API** + **SignalR** (strumień zdarzeń agenta, statusy).
- **Clean Architecture, ale KISS**: bez CQRS/MediatR (zwykłe serwisy aplikacyjne), bez event sourcingu, bez mikroserwisów. Jedna klasa = jedna odpowiedzialność, krótkie metody, czytelne nazwy.
- **EF Core + SQLite** (łatwa zmiana na SQL Server).
- Definicje typów zadań i flow jako **YAML** w repo + cache w bazie.
- Testy: **xUnit** + **NSubstitute**.
- `Directory.Packages.props`, `nullable enable`, `TreatWarningsAsErrors`.
- Logowanie: `ILogger` + Serilog (plik + konsola).
- Konfiguracja: `appsettings.json` + `IOptions<T>`. Sekrety: user-secrets / zmienne środowiskowe. **Nigdy** w repo.

**Frontend**
- **Vue 3 (Composition API, `<script setup>`) + TypeScript + Vite**, Pinia (stan), Vue Router.
- Jedna lekka biblioteka komponentów (do wyboru: PrimeVue / Vuetify / Naive UI).
- Typy DTO generowane lub trzymane w jednym miejscu (`src/api/`), klient HTTP za cienką warstwą.
- Testy: **Vitest** + **Vue Test Utils** dla kluczowych komponentów.
- ESLint + Prettier, `strict` w `tsconfig`.

### Struktura solucji

```
FlowHub.sln
src/
  FlowHub.Domain/          # encje, value objects, enumy, reguły. ZERO zależności.
  FlowHub.Application/     # use case'y (serwisy), interfejsy portów, DTO, silnik flow
  FlowHub.Infrastructure/  # EF Core, Copilot (SDK/CLI + Fake), Git, pliki, YAML
  FlowHub.Integrations/    # TeamCity, Octopus, TFS, tracker, dokumentacja — za interfejsami (+ Fake)
  FlowHub.Api/             # ASP.NET Core Web API, SignalR hub, DI, hosting
web/                       # Vue 3 + TS (Vite)
tests/
  FlowHub.Domain.Tests/
  FlowHub.Application.Tests/
  FlowHub.Api.Tests/
flows/                     # definicje YAML typów zadań i flow
blocks/                    # biblioteka bloczków: skrypty, agenci, skille, prompty
```

Kierunek zależności: `Api → Application → Domain`, `Infrastructure/Integrations → Application`. Domain nie zna nikogo.

## 4. Model domeny (startowy)

- **TaskType** — np. `Business`, `Bug`, `Technical`, `SpikeBusiness`, `SpikeTechnical`, `SpikeBug`. Wskazuje **FlowDefinition**. NIE enum — YAML (nowe typy bez kompilacji).
- **FlowDefinition** — nazwa, wersja, lista **StepDefinition** (kolejność, warunki, wymaga akceptacji).
- **StepDefinition (bloczek)** — `Id`, `Name`, `Kind`, `Inputs`, `Outputs`, `DefaultModel`, `TokenBudget`, `RequiresApproval`, `AllowedTools`.
- **StepKind**: `Script` (bez AI, 0 tokenów), `Agent` (Copilot z custom agentem/skillem/promptem), `Manual` (checklista), `Check` (stan z zewnątrz: TC, Octopus, testy).
- **WorkItem** — typ, tytuł, link do źródła, katalog roboczy, status, aktualny krok, **lista TaskRepo (1..N)**.
- **Repository** (katalog repo, globalny) — nazwa, URL w TFS, domyślny branch, build configuration(s) TeamCity, projekt Octopus, słowa kluczowe / moduły. UI „Repozytoria” lub `repos.yaml`.
- **TaskRepo** — zadanie ↔ repo: branch zadania, ścieżka lokalna, rola (główne / zależne), **osobny stan per repo**: PR, buildy TC, wdrożenia Octopus, testy, diff, tokeny.
- **StepRun** — status, propozycja, moja edycja, wynik, logi, czas, **TokenUsage**.
- **TokenUsage** — model, input, output, cache read/write, koszt, czas.
- **Artifact** — plik wynikowy kroku (`context.md`, `plan.md`, `diff.patch`).
- **Retro** — wnioski, propozycje zmian w agentach/skillach/flow.

Statusy kroku: `Pending → Proposed → Approved → Running → Succeeded | Failed | Skipped`. Przejścia pilnuje Domain (nie UI).

## 5. Integracja z GitHub Copilot

### Preferowane: GitHub Copilot SDK dla .NET
Sprawdź aktualny pakiet NuGet (SDK steruje Copilot CLI przez JSON-RPC):
- sesje (model, katalog roboczy),
- **strumień zdarzeń** (`assistant.message_delta`, `tool.execution_start/complete`, `session.idle`) → na żywo w UI,
- **`assistant.usage`** → `TokenUsage`,
- **handler uprawnień** → pytanie w UI przed ryzykownym narzędziem (`shell`, `write`).

### Fallback: Copilot CLI nieinteraktywnie
`copilot -p "<prompt>" --model <model> --output-format json --agent <nazwa> --allow-tool '<lista>' --share <ścieżka.md>`
- JSONL ze stdout linia po linii → te same zdarzenia co SDK,
- **nie używaj** `--allow-all` / `--yolo`.

> Zweryfikowane w docs (2026-11-11) — szczegóły i komendy: [docs/copilot-cli.md](docs/copilot-cli.md).
> - SDK: paczka `GitHub.Copilot.SDK`; zdarzenie z tokenami nie jest potwierdzone → sprawdzić w Fazie 4.
> - CLI nie czyta `.github/prompts/*.prompt.md` → prompty w `blocks/prompts/` to szablony FlowHub.
> - Agenci: `.github/agents/<nazwa>.agent.md`; skille: `.github/skills/<nazwa>/SKILL.md`.

### Fake (na start)
`FakeAgentRunner` — symuluje strumień zdarzeń (delty, narzędzia, prośbę o zgodę, usage) z opóźnieniami, żeby UI i silnik działały bez Copilota.

### Abstrakcja
```csharp
public interface IAgentRunner
{
    IAsyncEnumerable<AgentEvent> RunAsync(AgentRequest request, CancellationToken ct);
}
```
`AgentEvent` = `MessageDelta | ToolStarted | ToolFinished | Usage | PermissionRequested | Completed | Failed`. UI i silnik flow znają tylko ten interfejs.

## 6. Optymalizacja tokenów

1. **Najpierw skrypt, potem AI.** Katalog, klon, `git diff`, lista paczek, wynik testów → `Script`.
2. **Analiza raz, używana wiele razy.** „Analiza technologii” → `context.md`; kolejni agenci dostają plik zamiast skanować repo.
3. **Planowanie** (tani model) proponuje per krok: model, budżet, pliki w kontekście. **Edytowalne.**
4. **Routing modeli:** `prosty/średni/trudny → model` w konfiguracji. Domyślnie najtańszy; plan może podnieść z uzasadnieniem.
5. **Budżet na krok i zadanie.** 80% → ostrzeżenie. 100% → pauza i pytanie.
6. **Krótkie konteksty:** `summary.md` (max ~300 słów) zamiast całej rozmowy.
7. **Ograniczone narzędzia** per krok (`AllowedTools`).
8. **Raport:** tokeny i koszt per krok, zadanie, typ zadania, model. Wykres trendu.

## 7. Flow i bloczki

### Przykładowy flow (`Bug`)
1. `Script` — katalog `work/<ID>-<slug>/`
2. `Script/Check` — treść zadania z trackera
3. `Agent` (tani) — rozpoznanie repo: najpierw słowa kluczowe (0 tokenów), model tylko gdy trzeba → lista z uzasadnieniem. **Zatwierdzam / dodaję / usuwam.**
4. `Script` — per repo: klon/odśwież, branch zadania (ta sama nazwa wszędzie)
5. `Script` — kopiuj agenty, skille, prompty, skrypty do każdego repo (`.github/agents`, `.github/skills`, `.github/prompts`)
6. `Agent` (tani) — analiza technologii per repo → `context-<repo>.md` + zależności między repo → `context.md`
7. `Agent` (tani) — plan: kroki, modele, budżety, co i w jakiej kolejności w którym repo, **Definition of Done** → `plan.md`
8. `Agent` (wg planu) — reprodukcja / przyczyna
9. `Agent` — poprawka + testy (per repo)
10. `Script/Check` — build + testy lokalnie, per repo
11. `Check` — TFS: PR per repo
12. `Check` — TeamCity: buildy, testy per repo
13. `Check` — Octopus: wdrożenie per projekt, link do środowiska
14. `Agent` (tani) — `how-to-test.md` (z kolejnością wdrożeń)
15. `Agent` (tani) — szkic dokumentacji (do akceptacji)
16. `Agent` (tani) — **retro**

Kroki „per repo” silnik uruchamia dla każdego TaskRepo. `Script`/`Check` mogą równolegle, `Agent` domyślnie po kolei (konfigurowalne).

### Każdy krok (human-in-the-loop)
```
Propozycja → [Edytuj] → [Zatwierdź] / [Pomiń] / [Zmień model] → Uruchom → Podgląd wyniku → [Akceptuj] / [Powtórz z uwagą]
```
Propozycja: co zrobię, jakim modelem, szacowany koszt, jakie pliki w kontekście. Prompt edytowalny przed uruchomieniem.

### Edytor flow
- Bloczki z biblioteki → dodaj, zmień kolejność (drag & drop lub strzałki — KISS).
- Nowy typ zadania = nowy YAML (UI lub ręcznie).
- Walidacja YAML przy starcie i zapisie (czytelny błąd).

```yaml
type: Bug
name: Błąd
flow:
  - id: create-folder
    kind: Script
    script: blocks/scripts/create-folder.ps1
  - id: analyze-tech
    kind: Agent
    agent: tech-analyzer
    model: tier:cheap
    tokenBudget: 30000
    requiresApproval: true
    allowedTools: [read, shell(git:*)]
    outputs: [context.md]
```

## 8. Dashboard (CRM)

Lista zadań (karty/tabela) + szczegóły zadania:
- **Pasek etapów** flow — gdzie jesteśmy, co czeka na akceptację (wyraźnie).
- **Konsola agenta na żywo** — komunikaty, narzędzia, prośby o zgodę.
- **Testy lokalne**: zielone/czerwone, liczba.
- **PR w TFS (Azure DevOps Server)**: status (Draft/Active/Completed/Abandoned), recenzenci i głosy, nierozwiązane komentarze, polityki brancha, merge status, link.
- **TeamCity**: pending changes z brancha, buildy z moimi zmianami + status, nieudane testy (nazwy + link), link.
- **Octopus**: release z moimi zmianami, tabela środowisko × wersja × status (DEV/TEST/UAT/PROD), ostatnie wdrożenie + czas, linki.
- **Tabela repo w zadaniu** (najważniejsza):

  | Repo | Branch | Testy | PR (TFS) | Build (TC) | Release | DEV | TEST | Akcje |
  |---|---|---|---|---|---|---|---|---|
  | Billing.Api | feature/123 | ✅ 120/120 | 🟡 2/3 akcept. | ✅ #4512 | 1.8.3 | ✅ | ⏳ | Diff · VS Code |
  | Shared.Lib | feature/123 | ✅ 40/40 | ✅ Completed | ✅ #988 | 2.1.0 | ✅ | ✅ | Diff · VS Code |

  Komórka: ikona + kolor + tekst + link. Klik wiersza → szczegóły repo.
- **Podsumowanie** nad tabelą: „2 z 3 repo gotowe na TEST”; najgorszy status wygrywa.
- Na liście zadań — kropki per repo (co blokuje).
- Odświeżanie co 1–2 min (BackgroundService) + „Odśwież teraz”. Cache wyników.
- **Paczki**: outdated / vulnerable (`dotnet list package --outdated --vulnerable`).
- **Tokeny i koszt**: suma + per krok.
- Przyciski: **Otwórz w VS Code** (`vscode://file/...`), **Pokaż diff** (side-by-side), **Jak przetestować**.

UI: duże wyraźne statusy (kolor + ikona + tekst), mało tekstu, dobry kontrast, bezszeryfowa czcionka.

## 9. Integracje (każda za interfejsem, na start Fake)

```csharp
public interface ITaskSource { Task<TaskDetails> GetAsync(string id, CancellationToken ct); }

public interface IPullRequestService   // TFS / Azure DevOps Server
{
    Task<PullRequestStatus?> GetForBranchAsync(string repo, string branch, CancellationToken ct);
}

public interface ICiServer             // TeamCity
{
    Task<IReadOnlyList<CiChange>> GetChangesAsync(string branch, CancellationToken ct);
    Task<IReadOnlyList<BuildStatus>> GetBuildsWithChangesAsync(string branch, CancellationToken ct);
}

public interface IDeployServer         // Octopus
{
    Task<DeploymentOverview> GetOverviewAsync(string project, string releaseOrBranch, CancellationToken ct);
}

public interface IDocsPublisher { Task<Uri> PublishDraftAsync(DocPage page, CancellationToken ct); }
```

- Najpierw **Fake** (realistyczne dane testowe, różne statusy, losowe zmiany w czasie), żeby UI działało od razu.
- Przełączanie w `appsettings.json`, np.:
  ```json
  "Integrations": { "TaskSource": "Fake", "PullRequests": "Fake", "Ci": "Fake", "Deploy": "Fake", "Docs": "Fake", "AgentRunner": "Fake" }
  ```
- Potem prawdziwe:
  - **TFS / Azure DevOps Server** — REST (`_apis/git/repositories/{repo}/pullrequests`, polityki, wątki), PAT. Sprawdź wersję API serwera.
  - **TeamCity** — REST (`/app/rest/changes`, `/app/rest/builds?locator=branch:...`), token.
  - **Octopus** — REST (releases, deployments, environments, dashboard), API key.
  - tracker i dokumentacja (wg odpowiedzi).
- Mapowanie: **repo → (katalog) build configuration TC + projekt Octopus**, potem po **branchu i hashu commita** (commit → PR → build → release). Opisz mapowanie przed implementacją.
- „Importuj” katalog repo: lista repo z TFS, build configs z TC (po VCS root URL), projekty Octopus → **propozycja** dopasowań do zatwierdzenia.

## 10. Retro

Agent (tani) dostaje: `plan.md`, podsumowania kroków, statystyki tokenów, błędy, moje poprawki → `retro.md`:
- co poszło dobrze / źle,
- które kroki zużyły najwięcej tokenów i dlaczego,
- **konkretne propozycje zmian** w agentach, skillach, promptach, flow (jako diff).

Każdą propozycję zatwierdzam osobno → zmiana do `blocks/` (commit).

## 11. Fazy (po każdej STOP i akceptacja)

- **Faza 1 — Szkielet:** solucja, projekty, DI, Serilog, EF Core + SQLite, Web API + SignalR hub, Vue (Vite + TS + router + Pinia) z pustym Dashboardem, testy (xUnit + Vitest) działają.
- **Faza 2 — Domena + YAML:** encje (WorkItem → wiele TaskRepo), katalog repo, statusy z regułami przejść, wczytywanie i walidacja YAML. Testy domeny.
- **Faza 3 — Silnik flow:** kroki `Script` i `Manual`, propozycja → edycja → akceptacja. Katalog zadania.
- **Faza 4 — Copilot:** `IAgentRunner` (Fake, SDK, fallback CLI), konsola na żywo, prośby o zgodę, `TokenUsage`.
- **Faza 5 — Optymalizacja:** `context.md`, planowanie, routing modeli, budżety, raport tokenów.
- **Faza 6 — Dashboard:** etapy, diff, VS Code, testy, paczki.
- **Faza 7 — Integracje:** Fake → TFS PR → TeamCity → Octopus → „ścieżka zmiany” → tracker → dokumentacja.
- **Faza 8 — Edytor flow + Retro.**

## 12. Pytania na start (krok 0)

1. Skąd zadania? (Jira / Azure DevOps / GitHub Issues / inne) — czy jest API token.
2. Gdzie dokumentacja? (Confluence / Azure DevOps Wiki / SharePoint / Markdown w repo)
3. TFS — wersja (Azure DevOps Server 2022?), Git czy TFVC? Jak TC i Octopus łączą build z releasem? Środowiska w Octopusie?
4. Model rozliczeń Copilot i dostępne modele (routing i koszty).
5. Tylko lokalnie dla mnie czy dla zespołu? (logowanie, bezpieczeństwo)
6. Windows czy Linux? PowerShell czy bash?
7. Własne custom agenty / skille Copilot do `blocks/`?

## 13. Bezpieczeństwo (nie negocjujemy)

- Żadnych sekretów w kodzie, YAML, logach ani promptach.
- Agent bez `--allow-all`. `shell` i `write` tylko z listy kroku i za zgodą w UI.
- Agent pracuje tylko w katalogu zadania.
- Dokumentacja i zmiany w `blocks/` — zawsze szkic do akceptacji.
- Żadnego `git push` bez zgody.

## 14. Decyzje (uzupełniane w trakcie)

### Krok 0 — odpowiedzi (2026-11-10)

**Środowisko:** Windows, PowerShell, .NET 10 (SDK 10.0.401), Node 24, aplikacja **lokalna, jednoosobowa** (bez logowania). Jeden proces: API serwuje zbudowany frontend (w dev: Vite proxy do API).

**Tracker zadań — dwa źródła:**
- **Nowa Jira** — REST API (adres + token w user-secrets).
- **Stara Jira** — brak API → **ręczne wprowadzenie** treści zadania w UI (`ManualTaskSource`).
- Każde zadanie wie, z którego źródła pochodzi.

**Dokumentacja:** Markdown w repo (bywa, że jej nie ma → krok dokumentacji tworzy ją jako szkic).

**TFS:** Azure DevOps Server, repo Git. **TeamCity:** numer buildu zawiera nazwę/numer brancha. Środowisko testowe: **DEV** (Octopus).

**Copilot:** model rozliczeń i lista modeli — pomijamy na razie; tabela routingu i ceny w konfiguracji, uzupełnimy później.

**UI:** **Vuetify 4** (MIT; zmiana z PrimeVue — wersja 5 jest płatna), ikony `@mdi/font`, nowoczesny dashboard CRM, **ciemny motyw w odcieniach fioletu**.

**Fake:** dane statyczne (bez symulacji zmian w czasie).

**Git:** repo zainicjowane lokalnie.

### Tryb pracy (2026-11-11)

Użytkownik zezwolił na **pracę autonomiczną przez kolejne fazy**: każda faza na osobnym branchu (`faza-N-...`), po zielonym `check.ps1` **lokalny PR = merge `--no-ff` do `main`** (bez `git push`). Decyzje podjęte samodzielnie zapisuję w [docs/DECYZJE.md](docs/DECYZJE.md) do przeglądu.

### Wymagania dodatkowe od użytkownika

1. **Kod czysty, modularny, KISS**, otwarty na moduły dodatkowe (nowe integracje / typy kroków dodawane bez ruszania rdzenia — rejestracja przez interfejsy + DI).
2. **Kanban zadań** — kolumny wg statusu pracy, np. *Do zrobienia → W pracy → Czeka (na coś) → Czeka na testy → Czeka na wdrożenie → Gotowe*. Wiele zadań równolegle. Kolumny konfigurowalne.
3. **Interaktywna nakładka na Copilot CLI** — konsola w UI połączona z CLI: widać na żywo, co się dzieje, można pisać do agenta. Każdy etap można podejrzeć, zmienić, zobaczyć wynik, przejść dalej.
4. **Jedno zadanie = jeden katalog projektu**, **każdy etap flow = osobna sesja Copilot** (kontekst przekazywany plikami: `context.md`, `plan.md`, `summary.md`).
5. **Retro** na końcu — skill, który podsumuje zadanie i sprawdzi, co się działo.
6. **Agenci i skille po polsku**, modularne (łatwe do edycji i dodawania), **uniwersalne** — kopiowane do każdego repo zadania, więc bez założeń o konkretnym repo.
7. Cel: podaję dane wejściowe, a aplikacja razem z Copilot CLI, **za moją akceptacją na każdym etapie**, przechodzi od wymagań do realizacji.

### Flow bazowy (od użytkownika)

1. Utworzenie folderu zadania
2. Pobranie repo do folderu
3. Utworzenie brancha
4. Analiza technologiczna → `context.md`
5. Planowanie → `plan.md`
6. Wybór modelu do realizacji (sugestia z planu, do zmiany)
7. Realizacja
8. Testowanie
9. Opis zmian dla architektów **per komponent** → `architecture-notes.md`
10. Mail do zainteresowanych **wg szablonu** → `email.md` (szkic, nic nie wysyłamy)
11. Wiadomość na **Teams** z linkiem do PR → `teams.md` (szkic)
12. Retro → `retro.md`

Szablony maila i Teams: `blocks/templates/` (edytowalne).

### Rozszerzenia po fazie 8 (2026-11-11, na prośbę użytkownika)

Fazy 9–12 — szczegóły i lista rzeczy niesprawdzonych w [docs/DECYZJE.md](docs/DECYZJE.md):

- **Ustawienia w aplikacji** (moduły `ISettingsModule`): Copilot CLI, plan i modele, folder repozytoriów, TFS, Jira, TeamCity, Octopus. Zapis poza repo (`%LOCALAPPDATA%\FlowHub`), tokeny szyfrowane, zmiany bez restartu. **Nowe połączenie = nowy moduł ustawień**, nie nowy wpis w `appsettings.json`.
- **Copilot CLI zainstalowany i sprawdzony** (1.0.109): runner `-p`, tokeny z `--usage-output-file`, powtórka kroku przez `--resume`. Plan „bez limitu” (Copilot Enterprise w pracy) wyłącza budżety.
- **Terminal Copilota w zadaniu** (xterm.js + ConPTY) — interaktywny CLI, także jako kontynuacja sesji kroku.
- **Pliki `.md` zadania edytowalne w aplikacji.**
- **Krok ręczny „co dalej?” po realizacji** (`followUp` w flow) — pętla polecenie → agent → przegląd.
- **Generator bloczków** (skill / agent / prompt / skrypt na wzór istniejącego) — zawsze szkic do akceptacji.
- **Repozytoria:** skan lokalnego folderu (np. `C:\tfs`) ze statusami PR / build / wdrożenia oraz wybór repo z TFS przy nowym zadaniu.
- **Do zrobienia w pracy:** prawdziwe klienty TeamCity i Octopus — po zebraniu próbek wg [docs/polaczenia-w-pracy.md](docs/polaczenia-w-pracy.md).
