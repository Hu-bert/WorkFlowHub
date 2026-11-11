---
name: analiza-repozytorium
description: Szybkie rozpoznanie nieznanego repozytorium - stack, struktura, komendy build i test, zależności. Użyj, gdy trzeba opisać repo albo przygotować plik kontekstu.
---

# Analiza repozytorium

Cel: opisać repo w ok. 400 słowach tak, żeby nikt nie musiał skanować go ponownie.

## Kolejność czytania (od najtańszego)

1. `README*`, pliki w katalogu głównym.
2. Solucja i projekty: `*.sln`, `*.slnx`, `*.csproj`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`.
3. Frontend: `package.json`, `tsconfig.json`, konfiguracja bundlera.
4. CI/CD: pliki pipeline'ów, `Dockerfile`, skrypty w `build/` lub `scripts/`.
5. Struktura katalogów (2 poziomy) - bez otwierania plików.
6. Dopiero na końcu: 2-3 reprezentatywne pliki kodu i testów, żeby złapać konwencje.

## Przydatne polecenia (tylko odczyt)

- `git log --oneline -15` - czym repo żyje ostatnio.
- `git ls-files | head -200` - co jest śledzone.
- `dotnet list package` / zawartość `package.json` - zależności.

## Czego szukać

- Wersje frameworków (TargetFramework, wersja Node, główne paczki).
- Dokładne komendy: build, test, uruchomienie.
- Wspólne biblioteki i kontrakty łączące to repo z innymi.
- Konwencje: nazwy projektów testowych, układ katalogów, styl.
- Pułapki: kod generowany, migracje bazy, pliki konfiguracyjne per środowisko.

## Zasady

- Niczego nie zmieniaj i nie instaluj.
- Nie uruchamiaj builda ani testów, jeśli nie jesteś o to proszony - to kosztuje czas.
- Nie cytuj sekretów ani connection stringów, nawet jeśli je zobaczysz.
