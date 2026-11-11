---
name: podsumowanie-kroku
description: Zwięzłe podsumowanie wykonanej pracy dla kolejnych agentów i dla człowieka. Użyj na końcu każdego kroku, który zmienia kod lub tworzy dokument.
---

# Podsumowanie kroku

Kolejny agent dostanie **tylko to podsumowanie**, nie całą rozmowę. Ma mu wystarczyć.

## Układ (maksymalnie ok. 300 słów)

```markdown
# Podsumowanie: <nazwa kroku> - <repo>

## Co zrobiono
- `ścieżka/pliku` - jedno zdanie.

## Testy
- Dodane/zmienione: ...
- Wynik: przeszły / nie przeszły (które i dlaczego).

## Odstępstwa od planu
- brak / opis i powód.

## Uwagi dla kolejnych kroków
- ...
```

## Zasady

- Fakty, nie opis procesu. Bez „najpierw sprawdziłem, potem…”.
- Podawaj ścieżki plików - po nich kolejny agent trafi od razu we właściwe miejsce.
- Jeśli czegoś nie udało się zrobić, napisz to w pierwszym punkcie, nie na końcu.
- Żadnych sekretów, tokenów ani haseł.
