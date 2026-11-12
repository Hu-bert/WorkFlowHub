# blocks/

Biblioteka bloczków kopiowana do każdego repo zadania. Wszystko **po polsku** i **uniwersalne** (wiedza o konkretnym repo pochodzi z `context.md`).

| Katalog | Trafia do repo zadania jako | Format |
|---|---|---|
| `agents/` | `.github/agents/<nazwa>.agent.md` | custom agent Copilot |
| `skills/` | `.github/skills/<nazwa>/SKILL.md` | skill Copilot |
| `prompts/` | — (wklejane przez FlowHub) | szablony promptów kroków |
| `scripts/` | — (uruchamiane przez FlowHub) | PowerShell z BOM (kroki `Script`); kontekst w zmiennych `FLOWHUB_*` |
| `templates/` | — | szablony maila i wiadomości na Teams |

Edycja i generator nowych bloczków: strona „Flow i bloczki” → Bloczki. Format plików Copilot: [docs/copilot-cli.md](../docs/copilot-cli.md).
