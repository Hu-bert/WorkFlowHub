namespace FlowHub.Infrastructure.Agents;

/// <summary>Treść dokumentów „pisanych” przez FakeAgentRunner — żeby flow i UI dało się obejrzeć bez Copilota.</summary>
internal static class FakeDocuments
{
    private const string Notice = "> Dokument demonstracyjny (FakeAgentRunner) — powstał bez udziału modelu.";

    public static string For(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var body = Bodies.FirstOrDefault(entry => name.StartsWith(entry.Prefix, StringComparison.OrdinalIgnoreCase)).Body
            ?? "## Wynik\n\nKrok wykonany.";

        return $"# {name}\n\n{Notice}\n\n{body}\n";
    }

    private static readonly (string Prefix, string Body)[] Bodies =
    [
        ("context", """
            ## Stack
            - .NET 10, C#, ASP.NET Core
            - Testy: xUnit

            ## Struktura
            - `src/` — kod aplikacji
            - `README.md` — opis repo

            ## Komendy
            - Build: `dotnet build`
            - Testy: `dotnet test`

            ## Zależności między repo
            - brak wykrytych
            """),
        ("root-cause", """
            ## Objawy
            Błąd opisany w task.md.

            ## Przyczyna
            Stawka wyliczana jest przed zastosowaniem zmiany płatności.

            ## Jak odtworzyć
            1. Utwórz fakturę.
            2. Zmień metodę płatności.
            3. Sprawdź stawkę.
            """),
        ("plan", """
            ## Zakres
            1. Poprawka w repo głównym.
            2. Test regresyjny.

            ## Kolejność między repo
            Najpierw biblioteka wspólna, potem serwis.

            ## Sugerowany model do realizacji
            `tier:standard` — zmiana w jednym module, średnia trudność. Budżet: 150 000 tokenów.

            ## Definition of Done
            - [ ] Testy przechodzą lokalnie
            - [ ] PR utworzony we wszystkich repo
            - [ ] Opis zmian dla architektów gotowy
            """),
        ("summary", """
            ## Co zrobiono
            - Dodano zmianę demonstracyjną w README.md.

            ## Testy
            - Nie uruchamiano (tryb demonstracyjny).

            ## Uwagi dla kolejnych kroków
            - brak
            """),
        ("architecture-notes", """
            ## Komponent: repo główne
            - **Co się zmieniło:** poprawiono wyliczanie stawki.
            - **Wpływ na kontrakty:** brak.
            - **Ryzyko:** niskie.

            ## Komponent: biblioteka wspólna
            - bez zmian.
            """),
        ("email", """
            **Temat:** [Zmiana] Poprawka wyliczania stawki

            Cześć,

            wdrażamy poprawkę opisaną w zadaniu. Zakres i wpływ — w załączonym opisie dla architektów.

            Pozdrawiam
            """),
        ("teams", """
            PR gotowy do przeglądu 🙏

            - Co: poprawka wyliczania stawki
            - Link do PR: (uzupełnij)
            - Jak przetestować: opis w zadaniu
            """),
        ("retro", """
            ## Co poszło dobrze
            - Kroki skryptowe przeszły bez udziału człowieka.

            ## Co poszło źle
            - brak danych (tryb demonstracyjny).

            ## Najdroższe kroki
            - Realizacja — najwięcej tokenów.

            ## Propozycje zmian
            - `blocks/prompts/planowanie.md`: dodać pytanie o zależności między repo.
            """),
        ("spike-report", """
            ## Pytanie
            Opisane w task.md.

            ## Wnioski
            - Rozwiązanie jest wykonalne w obecnej architekturze.

            ## Zadania, które z tego wynikają
            1. Zadanie techniczne: przygotowanie kontraktu.
            """),
    ];
}
