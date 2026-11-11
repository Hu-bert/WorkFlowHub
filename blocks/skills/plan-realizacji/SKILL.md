---
name: plan-realizacji
description: Układanie planu realizacji zadania programistycznego w jednym lub wielu repozytoriach, z doborem modelu i budżetu tokenów. Użyj przy planowaniu zmian przed implementacją.
---

# Plan realizacji

## Układ pliku `plan.md`

```markdown
# Plan: <klucz> - <tytuł>

## Cel
Jedno zdanie.

## Zakres
- Zmieniamy: ...
- Nie ruszamy: ...

## Kroki
### <nazwa repo>
1. `ścieżka/pliku` - co zrobić.

## Kolejność między repo
1. <repo> - dlaczego pierwsze.

## Testy
- ...

## Sugerowany model do realizacji
- Poziom: tier:cheap | tier:standard | tier:premium
- Budżet: <liczba> tokenów
- Uzasadnienie: jedno zdanie.

## Definition of Done
- [ ] ...

## Pytania i ryzyka
- ...
```

## Dobór modelu

| Poziom | Kiedy |
|---|---|
| `tier:cheap` | Zmiana w 1-2 plikach, jasny wzorzec w repo, brak zmian kontraktów. |
| `tier:standard` | Kilka plików lub modułów, nowe testy, logika biznesowa. |
| `tier:premium` | Zmiana przekrojowa, współbieżność, trudny błąd, wiele repo naraz. |

Zaczynaj od najtańszego poziomu. Podnieś go tylko z konkretnym powodem.

## Dobór budżetu

Szacuj: (liczba plików do przeczytania × ok. 2 tys. tokenów) + (liczba plików do zmiany × ok. 5 tys.) + zapas 30%.

## Zasady

- Najmniejsza zmiana, która spełnia zadanie.
- Krok planu = konkretny plik lub moduł, nie ogólnik („poprawić walidację”).
- Zmiany kontraktów (API, zdarzenia, schemat bazy, wspólna paczka) zawsze wypisz osobno - wyznaczają kolejność między repo.
