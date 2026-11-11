# FlowHub

Webowe „CRM zadań programistycznych”: tworzysz zadanie, a FlowHub prowadzi je krok po kroku (skrypty, agenci GitHub Copilot, kroki ręczne, sprawdzenia) — **z Twoją akceptacją na każdym etapie** — licząc tokeny i koszt.

- Specyfikacja i zasady pracy: [INSTRUKCJE.md](INSTRUKCJE.md)
- Decyzje podjęte po drodze i lista rzeczy niesprawdzonych: [docs/DECYZJE.md](docs/DECYZJE.md)
- Ściągawka Copilot CLI: [docs/copilot-cli.md](docs/copilot-cli.md)

## Uruchomienie

Wymagania: .NET 10 SDK, Node 24, Git, Windows PowerShell.

```powershell
./run.ps1          # buduje frontend i startuje aplikację: http://localhost:5080
./run.ps1 -Dev     # tryb dev: API + Vite z hot reload: http://localhost:5173
./check.ps1        # pełna weryfikacja: build + testy backendu i frontendu
```

Bez żadnej konfiguracji aplikacja działa na **atrapach (Fake)**: agent udaje sesję Copilot, a PR / TeamCity / Octopus / Jira zwracają dane testowe.

## Jak to działa

1. **Nowe zadanie** — klucz, tytuł, treść (z Jiry przez „Pobierz” albo wklejona ręcznie), typ zadania i repozytoria („Zaproponuj” dopasuje je po słowach kluczowych).
2. **Uruchom flow** — proste skrypty (folder, klon, branch, kopiowanie agentów) idą same; każdy inny krok czeka na „Zatwierdź”.
3. **Każdy krok**: propozycja (prompt do edycji, model, budżet) → Zatwierdź / Pomiń → konsola na żywo → wynik → dalej albo „Powtórz z uwagą”.
4. **Tabela repo** pokazuje dla każdego repo: testy, PR, build, release i środowiska. Najgorszy status wygrywa.
5. **Retro** na końcu podsumowuje zadanie i proponuje zmiany w bloczkach — wdrażasz je w zakładce „Flow i bloczki”.

## Co przełączyć w pracy

Połączenia ustawiasz na stronie **Ustawienia** w aplikacji — zmiany działają od razu, bez restartu. Ustawienia trafiają do `%LOCALAPPDATA%\FlowHub\settings.json`, a tokeny (zaszyfrowane) do `secrets.json` obok — nigdy do repo. Reszta jest w [src/FlowHub.Api/appsettings.json](src/FlowHub.Api/appsettings.json).

| Co | Gdzie | Uwagi |
|---|---|---|
| Repozytoria | [repos.yaml](repos.yaml) | Podmień wpisy `demo://` na prawdziwe adresy TFS, build configuration i projekty Octopus. |
| Agent Copilot | Ustawienia → GitHub Copilot CLI | Najpierw `copilot login` w zwykłym terminalu. Sprawdzone na Copilot CLI 1.0.109. |
| Plan i modele | Ustawienia → Modele i plan Copilot | „Bez limitu” (Enterprise) wyłącza budżety i wycenę. Poziomy domyślnie `auto`. |
| TFS (pull requesty, lista repo) | Ustawienia → TFS | Adres kolekcji, projekt, token PAT. Klient nie był sprawdzony na prawdziwym serwerze. |
| Jira | Ustawienia → Jira | Jak wyżej. Stara Jira bez API: zostaw atrapę i wklejaj treść ręcznie. |
| TeamCity, Octopus | Ustawienia | Na razie tylko zapis adresu i test połączenia; dane z atrapy. |
| Folder z repozytoriami | Ustawienia → Repozytoria lokalne | Np. `C:\tfs` — strona Repozytoria pokaże kopie z branchem, commitami i statusami. |

Co sprawdzić i zebrać przed podłączeniem serwerów: [docs/polaczenia-w-pracy.md](docs/polaczenia-w-pracy.md).
| Kolumny kanbana | `Board:Columns` | |
| Środowisko „gotowe” | `RepoStatus:ReadyEnvironment` | Domyślnie DEV. |

## Struktura

```
src/FlowHub.Domain          encje i reguły (zero zależności)
src/FlowHub.Application     use case'y, silnik flow, porty
src/FlowHub.Infrastructure  EF Core + SQLite, YAML, PowerShell, Git, agent (Fake / Copilot CLI)
src/FlowHub.Integrations    PR, CI, wdrożenia, tracker (Fake)
src/FlowHub.Api             Web API + SignalR + hosting frontendu
web/                        Vue 3 + TypeScript + Vuetify
flows/                      typy zadań i ich kroki (YAML)
blocks/                     agenci, skille, prompty, skrypty, szablony (po polsku)
tests/                      xUnit; frontend: Vitest w web/
```

## Rozszerzanie

| Chcę dodać… | Robię… |
|---|---|
| typ zadania | nowy plik w `flows/` albo „Nowy typ zadania” w UI |
| agenta / skill / prompt / skrypt | plik w `blocks/` albo zakładka „Bloczki” |
| rodzaj kroku | klasa `IStepExecutor` + jedna linia w `Application/DependencyInjection.cs` |
| integrację | implementacja portu z `Application/Integrations/Ports.cs` + wpis w `Integrations/DependencyInjection.cs` |
| inny sposób uruchamiania agenta (np. SDK) | klasa `IAgentRunner` + wpis w `Infrastructure/DependencyInjection.cs` |
| stronę w UI | widok w `web/src/views` + trasa w `router/index.ts` + pozycja w `layout/navigation.ts` |

## Bezpieczeństwo

- Agent nigdy nie dostaje `--allow-all`; narzędzia tylko z listy kroku, a przed `write` / `shell` pytanie o zgodę w UI.
- FlowHub nie robi `git push` i niczego nie wysyła (mail i Teams to szkice w plikach).
- Agenci i skille kopiowane do repo zadania trafiają do `.git/info/exclude` — nie wejdą do PR.
