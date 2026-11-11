---
name: opis-zmian-dla-architektow
description: Prosty opis zmian w systemie dla architektów, w podziale na komponenty. Użyj, gdy trzeba streścić zmianę dla osób, które nie będą czytać kodu.
---

# Opis zmian dla architektów

## Skąd brać fakty

1. `plan.md` - zamiar.
2. `summary-*.md` - wykonanie w każdym repo.
3. `git diff <branch-domyślny>...HEAD --stat` w każdym repo - zasięg zmian.
4. Pełny diff tylko dla plików dotykających kontraktów (kontrolery, DTO, zdarzenia, migracje).

## Układ pliku `architecture-notes.md`

```markdown
# Opis zmian: <klucz> - <tytuł>

## Komponent: <nazwa>
- **Co się zmieniło:** ...
- **Wpływ na kontrakty:** brak / opis
- **Zależności i kolejność wdrożenia:** ...
- **Ryzyko:** niskie | średnie | wysokie - dlaczego
- **Jak sprawdzić:** ...

## Podsumowanie
2-3 zdania.
```

## Zasady

- Komponent = to, co architekt rysuje na diagramie: serwis, biblioteka, baza, kolejka. Nie klasa.
- Mów, co system robi inaczej, a nie jakie metody zmieniono.
- Zmiana kontraktu zawsze na górze sekcji i z informacją, czy jest wstecznie zgodna.
- Ryzyko oceniaj uczciwie. „Niskie” wymaga uzasadnienia tak samo jak „wysokie”.
- Maksymalnie 5 punktów na komponent. Cały dokument: jedna strona.
