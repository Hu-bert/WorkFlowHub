# Decyzje podjęte samodzielnie (do przeglądu)

Lista rzeczy, które zdecydowałem sam podczas pracy autonomicznej. Każdą można cofnąć.

## Faza 1

| Decyzja | Dlaczego |
|---|---|
| **Vuetify 4 zamiast PrimeVue** (+ ikony `@mdi/font`) | PrimeVue 5 jest komercyjne (baner „Invalid PrimeUI License”), a linia 4.x to już stara gałąź. Użytkownik poprosił o darmową bibliotekę z lepszym wsparciem. Vuetify: MIT, aktywnie rozwijane, największa społeczność, komplet komponentów. Ikony MDI są w paczce — działają bez internetu. |
| **.NET 10 (LTS), paczki Microsoft.* 10.0.x** | Wyszedł .NET 11 (STS), spec mówi o LTS. |
| **Microsoft.Testing.Platform** (`global.json`) | .NET 10 SDK nie wspiera już VSTest dla xUnit v3. Testy: `dotnet test --solution FlowHub.slnx`. |
| **`FlowHub.slnx`** zamiast `.sln` | Domyślny format solucji w .NET 10. |
| Skrypty `.ps1` w UTF-8 z BOM, składnia zgodna z Windows PowerShell 5.1 | Na maszynie nie ma `pwsh`; 5.1 bez BOM psuje polskie znaki. |
