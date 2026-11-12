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

## Połączenia

Wszystko ustawiasz na stronie **Ustawienia** — zmiany działają od razu, bez restartu. Ustawienia, tokeny (zaszyfrowane), katalog repo i mapy repo leżą w `%LOCALAPPDATA%\FlowHub` — nigdy w repo. Dokładne ścieżki pokazuje karta „Gdzie są zapisane dane” w Ustawieniach.

| Co | Gdzie | Uwagi |
|---|---|---|
| Agent Copilot | Ustawienia → GitHub Copilot CLI | Najpierw `copilot login` w zwykłym terminalu. Sprawdzone na Copilot CLI 1.0.109. |
| Plan i modele | Ustawienia → Modele i plan Copilot | „Bez limitu” (Enterprise) wyłącza budżety i wycenę. Poziomy domyślnie `auto`. |
| TFS | Ustawienia → TFS | Pull requesty, lista repo i statusy buildów. Adres kolekcji + token PAT. |
| Buildy | Ustawienia → TeamCity / buildy | Źródło: statusy z TFS (bez mapowania) albo TeamCity REST. |
| Wdrożenia i środowiska | Ustawienia → Octopus Deploy | Także lista pokazywanych środowisk i środowisko domyślne. |
| Jira | Ustawienia → Jira | Stara Jira bez API: zostaw atrapę i wklejaj treść ręcznie. |
| Folder z repozytoriami | Ustawienia → Repozytoria lokalne | Np. `C:\tfs` — strona Repozytoria pokaże kopie jako drzewo, ze statusami. |
| Katalog repo | Repozytoria → Katalog repo | Skąd klonować repo i jak nazywa się w TeamCity / Octopus. Zapis w folderze ustawień; `repos.yaml` w repo to przykład startowy. |
| Kolumny kanbana | `Board:Columns` w [appsettings.json](src/FlowHub.Api/appsettings.json) | |

Klienty TFS, TeamCity, Octopus i Jira są napisane wg dokumentacji i **nie były sprawdzone na prawdziwych serwerach** — kroki podłączenia i diagnostyka: [docs/polaczenia-w-pracy.md](docs/polaczenia-w-pracy.md).
## Struktura

```
src/FlowHub.Domain          encje i reguły (zero zależności)
src/FlowHub.Application     use case'y, silnik flow, porty
src/FlowHub.Infrastructure  EF Core + SQLite, YAML, PowerShell, Git, agent (Fake / Copilot CLI)
src/FlowHub.Integrations    TFS, TeamCity, Octopus, Jira (+ atrapy)
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
| integrację | implementacja portu z `Application/Integrations/Ports.cs` + wpis w `Integrations/DependencyInjection.cs` + karta ustawień (`ISettingsModule`) |
| inny sposób uruchamiania agenta (np. SDK) | klasa `IAgentRunner` + wpis w `Infrastructure/DependencyInjection.cs` |
| stronę w UI | widok w `web/src/views` + trasa w `router/index.ts` + pozycja w `layout/navigation.ts` |

## Bezpieczeństwo

- Agent nigdy nie dostaje `--allow-all`; narzędzia tylko z listy kroku, a przed `write` / `shell` pytanie o zgodę w UI.
- FlowHub nie robi `git push` i niczego nie wysyła (mail i Teams to szkice w plikach).
- Agenci i skille kopiowane do repo zadania trafiają do `.git/info/exclude` — nie wejdą do PR.
