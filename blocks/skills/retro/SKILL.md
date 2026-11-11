---
name: retro
description: Retrospektywa zakończonego zadania programistycznego. Użyj, gdy trzeba podsumować zadanie, ocenić koszt kroków i zaproponować zmiany w agentach, skillach, promptach lub flow.
---

# Retro zadania

## Wejście

- `plan.md` - co było do zrobienia.
- `summary-*.md` - co faktycznie zrobiono w każdym repo.
- Statystyki z promptu: tokeny i koszt per krok, liczba prób, błędy, uwagi użytkownika.

## Jak analizować

1. **Plan a wykonanie** - co poszło zgodnie z planem, co nie i dlaczego.
2. **Powtórki** - każdy krok z więcej niż jedną próbą: co było nie tak za pierwszym razem? Czy uwaga użytkownika powinna na stałe trafić do promptu lub agenta?
3. **Koszt** - trzy najdroższe kroki. Dla każdego: czy dało się taniej (tańszy model, mniej plików w kontekście, skrypt zamiast agenta)?
4. **Błędy** - kroki zakończone błędem: przyczyna po stronie zadania, promptu, narzędzi czy środowiska?

## Układ pliku `retro.md`

```markdown
# Retro: <klucz zadania>

## Co poszło dobrze
- ...

## Co poszło źle
- ...

## Najdroższe kroki
| Krok | Tokeny | Koszt | Dlaczego tyle | Jak taniej |
|---|---|---|---|---|

## Propozycje zmian
### 1. <krótki tytuł>
- Plik: `blocks/...` lub `flows/...`
- Powód: ...
- Zmiana:
  ```diff
  - stara linia
  + nowa linia
  ```
```

## Zasady

- Maksymalnie 5 propozycji, od najbardziej opłacalnej.
- Propozycja bez wskazanego pliku i konkretnej zmiany nie jest propozycją - pomiń ją.
- Nie proponuj zmian „na zapas”. Tylko to, co wynika z tego zadania.
- Całość: maksymalnie ok. 500 słów.
