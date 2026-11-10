# Decyzje podjęte samodzielnie (do przeglądu)

Lista rzeczy, które zdecydowałem sam podczas pracy autonomicznej. Każdą można cofnąć.

## Faza 1

| Decyzja | Dlaczego |
|---|---|
| **PrimeVue 4.5.5 zamiast 5.x** (+ `primeicons` 7, `@primeuix/themes` 2) | PrimeVue 5 i primeicons 8 są komercyjne (baner „Invalid PrimeUI License”). Wersje 4.x / 7.x są na licencji MIT. Wersje przypięte na sztywno w `package.json`. |
| **.NET 10 (LTS), paczki Microsoft.* 10.0.x** | Wyszedł .NET 11 (STS), spec mówi o LTS. |
| **Microsoft.Testing.Platform** (`global.json`) | .NET 10 SDK nie wspiera już VSTest dla xUnit v3. Testy: `dotnet test --solution FlowHub.slnx`. |
| **`FlowHub.slnx`** zamiast `.sln` | Domyślny format solucji w .NET 10. |
| Skrypty `.ps1` w UTF-8 z BOM, składnia zgodna z Windows PowerShell 5.1 | Na maszynie nie ma `pwsh`; 5.1 bez BOM psuje polskie znaki. |
