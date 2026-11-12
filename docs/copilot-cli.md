# Copilot CLI — ściągawka

Źródło: oficjalna dokumentacja GitHub i `copilot --help` (CLI 1.0.109, sprawdzone 2026-11-11). Sekcja „Sprawdzone na prawdziwym CLI” na końcu opisuje to, na czym opiera się FlowHub.

- https://docs.github.com/en/copilot/reference/cli-command-reference
- https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-custom-agents-for-cli
- https://docs.github.com/en/copilot/concepts/agents/about-agent-skills
- https://github.com/github/copilot-sdk (dotnet/README.md)

## Instalacja i logowanie

```powershell
winget install GitHub.Copilot          # lub: npm install -g @github/copilot
copilot update                         # aktualizacja
copilot login                          # logowanie (w sesji: /login)
```

Token w zmiennej środowiskowej (kolejność): `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, `GITHUB_TOKEN`. Fine-grained PAT z uprawnieniem „Copilot Requests” (klasyczny `ghp_` nie działa).

## Tryb nieinteraktywny (to, czego używa FlowHub)

```powershell
copilot -p "Przeanalizuj repo i zapisz context.md" `
  --agent analiza-technologii `
  --model auto `
  --output-format json `
  --allow-tool 'read' --allow-tool 'shell(git:*)' `
  --deny-tool 'shell(git push)' `
  --allow-tool 'write(C:\work\123-bug\context.md)' `
  --usage-output-file usage.json --no-ask-user --no-auto-update --no-color
```

| Flaga | Do czego |
|---|---|
| `-p, --prompt` | prompt i wyjście po zakończeniu |
| `-i, --interactive` | start sesji interaktywnej z promptem |
| `-s, --silent` | tylko odpowiedź, bez dodatków |
| `--output-format json` | **JSONL** — jedno zdarzenie na linię (parsujemy w FlowHub) |
| `--model <id>` / `auto` | wybór modelu |
| `--agent <nazwa>` | custom agent z `.github/agents/` |
| `--allow-tool` / `--deny-tool` | pozwolenia na narzędzia (deny wygrywa) |
| `--available-tools` / `--excluded-tools` | jakie narzędzia w ogóle widzi model (krótszy prompt systemowy) |
| `--add-dir <dir>` | dodatkowy katalog z dostępem |
| `-C <dir>` | katalog roboczy |
| `--share <plik.md>` | zapis transkryptu do Markdown |
| `--usage-output-file <plik>` | zużycie tokenów i kredytów po zakończeniu sesji (JSON) |
| `-r, --resume [id]` / `--continue` | wznowienie sesji / ostatniej sesji |
| `-n, --name <nazwa>` / `--session-id <id>` | nazwanie sesji / własne ID |
| `--mode plan` / `--plan` | tryb planowania (bez zmian w plikach) |
| `--reasoning-effort <poziom>` | poziom „wysiłku” rozumowania |
| `--log-level debug` / `--log-dir <dir>` | logi |
| `--no-custom-instructions` | ignoruj AGENTS.md / copilot-instructions.md |
| `--no-ask-user` | agent nie zadaje pytań (dla automatyzacji) |
| `--trust-folder` | zaufaj katalogowi bez pytania |

**Nie używamy:** `--allow-all`, `--yolo`, `--allow-all-tools`, `--allow-all-paths`, `--autopilot`.

### Wzorce narzędzi

| Wzorzec | Znaczenie |
|---|---|
| `read` | odczyt plików |
| `write` / `write(README.md)` | zapis (wszędzie / konkretny plik) |
| `shell` | dowolna komenda (unikać) |
| `shell(git:*)` | wszystkie podkomendy git |
| `shell(dotnet test)` | dokładnie ta komenda |
| `url(github.com)` | dostęp do domeny |
| `memory` | pamięć agenta |
| `<mcp-server>(<tool>)` | narzędzie MCP |

## Komendy w sesji interaktywnej

| Komenda | Do czego |
|---|---|
| `/model [id]` | zmiana modelu |
| `/agent` | wybór custom agenta |
| `/skills list \| info <n> \| reload` | skille |
| `/usage` | **zużycie: tokeny per model, kredyty** |
| `/context` | **ile tokenów zajmuje kontekst** |
| `/compact [fokus]` | kompresja historii (tańsze kolejne odpowiedzi) |
| `/clear` (`/new`) | nowa rozmowa |
| `/plan [prompt]` | plan bez zmian w plikach |
| `/diff` | zmiany w plikach w tej sesji |
| `/undo`, `/rewind` | cofnięcie zmian |
| `/review` | przegląd zmian |
| `/session info \| files \| plan \| rename` | informacje o sesji (m.in. ścieżka logów) |
| `/share file \| html` (`/export`) | eksport transkryptu |
| `/resume [id]` | przełączenie na inną sesję |
| `/add-dir <dir>`, `/list-dirs`, `/cwd <dir>` | katalogi |
| `/permissions show \| reset` | podgląd / reset pozwoleń |
| `/instructions` | jakie pliki instrukcji są wczytane |
| `/mcp list \| add` | serwery MCP |
| `/init` | wygeneruj instrukcje dla repo |
| `/help` | pomoc |

Skróty w polu wpisywania: `!komenda` — uruchom w shellu, `@plik` — dołącz plik, `#123` — dołącz issue/PR, `Shift+Tab` — przełącz tryb (plan / autopilot).

## Custom agenci

- Repo: `.github/agents/<nazwa>.agent.md`; użytkownik: `~/.copilot/agents/` (nadpisuje repo).
- ID = nazwa pliku bez `.agent.md`.
- Frontmatter: `name`, `description`, `tools` (np. `["read", "grep", "glob", "edit", "execute"]`), `model`. Lista `tools` ogranicza narzędzia agenta — bez `edit` nie zapisze pliku.

```markdown
---
name: analiza-technologii
description: Analizuje stack, strukturę i komendy build/test repozytorium.
tools: ["read", "grep", "glob", "edit"]
---
Jesteś analitykiem technologii. ...
```

## Skille

- Repo: `.github/skills/<nazwa>/SKILL.md` (także `.claude/skills/`, `.agents/skills/`); użytkownik: `~/.copilot/skills/`.
- Frontmatter: `name` (wymagane, małe litery i myślniki), `description` (wymagane), `allowed-tools` (opcjonalne — nie dawać `shell`).
- Wywołanie: automatycznie lub `/nazwa-skilla` w prompcie.

## Instrukcje

CLI czyta i łączy: `.github/copilot-instructions.md`, `.github/instructions/**/*.instructions.md`, `AGENTS.md`, `~/.copilot/copilot-instructions.md`.
Plików `.github/prompts/*.prompt.md` CLI nie czyta — we FlowHub prompty to nasze szablony (`blocks/prompts/`), wklejane przez aplikację.

## Gdzie są dane sesji

- `~/.copilot/session-state/<session-id>/events.jsonl` — zdarzenia sesji (do odczytu historii / tokenów).
- `~/.copilot/logs/` — logi procesu.
- `~/.copilot/settings.json` — `model`, `logLevel`, `defaultPermissionMode`.
- `COPILOT_HOME` zmienia cały katalog.

## Copilot SDK dla .NET (nieużywany)

FlowHub steruje CLI bezpośrednio. Gdyby był potrzebny SDK: paczka `GitHub.Copilot.SDK` (`CopilotClient` → `CreateSessionAsync` → zdarzenia sesji), jako drugi `IAgentRunner`. Uprawnienia zawsze `ApproveOnce()` / `Reject()`, nigdy `ApproveAll`.
## Sprawdzone na prawdziwym CLI (1.0.109, 2026-11-11)

- `copilot -p … --output-format json`: każda linia to `{"type": …, "data": {…}}`. Używamy: `session.start` (`sessionId`), `assistant.message_delta` (`deltaContent`), `assistant.message` (`content`; puste przy samym wywołaniu narzędzia), `tool.execution_start` (`toolCallId`, `toolName`, `arguments`), `tool.execution_complete` (`toolCallId`, `success` — bez nazwy narzędzia), `result` (`exitCode`).
- **Tokenów nie ma w strumieniu.** `--usage-output-file <plik>` zapisuje na końcu JSON z `modelMetrics.<model>.usage` (`inputTokens` łącznie z cache, `outputTokens`, `cacheReadTokens`, `cacheWriteTokens`) i `totalNanoAiu` (10^9 = 1 kredyt AI).
- W trybie `-p` CLI **nie pyta o zgodę** — narzędzie spoza `--allow-tool` jest odrzucane. `write(<pełna ścieżka>)` pozwala zapisać jeden konkretny plik.
- `--agent <nazwa>` wczytuje `.github/agents/<nazwa>.agent.md` z katalogu roboczego; lista `tools` we frontmatterze ogranicza narzędzia agenta (bez `edit` agent nie zapisze pliku).
- Kontynuacja rozmowy: `--resume=<sessionId>` (także w trybie interaktywnym). `--model auto` = wybór Copilota.
- Instalator Windows kładzie CLI w `%LOCALAPPDATA%\GitHubCopilotCLI\copilot.exe`; FlowHub szuka tam, gdy `copilot` nie ma w PATH.
- Modeli i planu konta CLI nie wypisuje poza sesją (`/model` i `/usage` działają tylko interaktywnie). Zdarzenie `session.auto_mode_resolved` podaje `chosenModel` i `availableModels` — z tego FlowHub buduje listę podpowiedzi modeli.
