# Testare

Regulile obligatorii de verificare sunt în [AGENTS.md › Verificarea livrării](../AGENTS.md#verificarea-livrării). Acest document descrie testele existente, comenzile și verificările manuale.

## Strategia

- Regulile de business și serviciile sunt acoperite de teste unitare automate, independente de SQL Server; repository-urile sunt înlocuite de stub-uri scrise manual.
- Persistența (EF Core, SQL), paginile Razor și JavaScript nu au teste automate; ele se verifică manual, în browser, pe SQL Server — cu și fără JavaScript, pe mobil și în cele trei limbi —, cum s-a procedat la fiecare pas din version_0.01 și version_0.02.
- Catalogul .resx se verifică automat cu `tools/Test-Resources.ps1`.
- Testele verifică reguli și cazuri relevante, nu copii ale implementării.

## Proiectul de teste

`Solution/WorkNotes.Business.Tests` — xUnit 2.9.3, `xunit.runner.visualstudio` 3.1.4, `Microsoft.NET.Test.Sdk` 17.14.1; referă numai `WorkNotes.Business`.

| Clasa de teste | Acoperă | Teste executate (2026-09-25) |
| --- | --- | --- |
| `AccountRulesTests` | numele (diacritice, lungime, caractere de control), e-mailul | 11 |
| `ApplicationVersionServiceTests` | versiunea numerică maximă, tabela goală, anularea, erorile de bază de date | 8 |
| `ContextMemberServiceTests` | adăugarea după e-mail, rolul `Member`, drepturile proprietarului, eliminarea membrilor | 14 |
| `NoteRulesTests` | previzualizarea cardurilor (paragrafe, limită, tăiere la cuvânt) | 4 |
| `NoteServiceTests` | crearea notelor și ziua locală a jurnalului, gruparea pe luni și ordinea, salvarea paragrafelor, redenumirea, ștergerea, schimbul ordinii, conflictele, anularea | 39 |
| `WorkContextServiceTests` | normalizarea, validarea, proprietarul, filtrul de membru, anularea | 22 |
| **Total** | | **98** |

## Teste unitare — convenții

- O clasă `…Tests` pentru fiecare serviciu sau clasă de reguli; metodele de test au nume-propoziție în engleză (`OnlyTheOwnerSaves`, `SeveralJournalsPerDayAreAllowed`).
- `[Fact]` pentru un caz, `[Theory]` cu `[InlineData]` pentru variante.
- Dependențele sunt stub-uri `private sealed class` în clasa de teste, care înregistrează apelurile (`WasCalled`, `ReceivedToken`) și întorc rezultate fixe; nu se folosește o bibliotecă de mocking.
- Timpul este controlat printr-un `TimeProvider` fix (`FixedTime`), inclusiv fusul orar, pentru ziua jurnalului și luna locală.
- Se testează explicit propagarea `CancellationToken`, oprirea înainte de accesul la date când cererea este anulată și faptul că o eroare a bazei nu devine rezultat gol.

## Teste de integrare și UI

- Nu există teste de integrare (EF Core pe SQL Server) și nici teste UI automate.
- TODO: Necesită clarificare — dacă se adaugă teste de integrare și pe ce bază; o bază de test nu trebuie să fie baza de lucru a utilizatorului, iar datele lui nu se șterg pentru testare.

## Baza de test

Testele existente nu au nevoie de bază de date. Nu există o configurație pentru o bază de test. TODO: Necesită clarificare — numele, instanța și modul de creare a unei baze de test, dacă vor exista teste de integrare.

## Comenzi

Din folderul `Solution`:

```powershell
dotnet tool restore
dotnet restore WorkNotes.sln
dotnet build WorkNotes.sln --no-restore
dotnet test WorkNotes.sln --no-build --no-restore

# o singură clasă de teste
dotnet test WorkNotes.sln --no-build --no-restore --filter "FullyQualifiedName~NoteServiceTests"

# catalogul .resx (PowerShell)
.\tools\Test-Resources.ps1
```

Build-ul și testele rulează și pe Linux cu .NET 10 SDK; `Test-Resources.ps1` are nevoie de PowerShell. Rezultatele ultimei rulări sunt în [CURRENT-STATUS.md](CURRENT-STATUS.md#ultimul-build-și-ultimele-teste).

## Scenarii critice

Scenarii care trebuie să rămână acoperite, prin teste automate unde regula este în Business și manual în rest:

- accesul: un context sau o notă din afara apartenenței răspunde 404; o acțiune rezervată proprietarului, cerută de un membru, răspunde 403; notele private ale altor membri nu apar pe tablă;
- numai proprietarul editează, redenumește, șterge și mută o notă; numai proprietarul contextului îl editează, îl șterge și îi gestionează membrii, iar proprietarul nu se poate elimina;
- un context cu note nu poate fi șters; numele contextului este unic;
- salvarea din editor păstrează identitatea și auditul paragrafelor nemodificate; o salvare dintr-un editor învechit primește conflict fără să suprascrie;
- schimbul prin drag-and-drop este permis numai în aceeași lună și același context; eșecul readuce ordinea anterioară;
- gruparea pe luni după ultima modificare și ordinea din lună (`Order`, apoi ultima modificare, crearea și `Id`);
- versiunea din footer: versiunea maximă, tabela goală, eroarea de conexiune;
- politica de parolă, blocarea după 5 încercări, mesajul unic la autentificare;
- cele trei limbi și fallback-ul românesc.

## Verificări manuale obligatorii

După modificările care le ating, înainte de predare:

1. Aplicația pornește și tabla se încarcă pe SQL Server; footerul afișează versiunea curentă.
2. Fluxurile atinse funcționează cu și fără JavaScript (overlay-urile se deschid din URL, formularele funcționează fără script).
3. Paginile atinse arată corect în română, engleză și poloneză, la 320px și pe desktop, inclusiv cu titluri lungi.
4. Navigarea cu tastatura, focusul vizibil, erorile de validare client și server, mesajele Identity și paginile protejate.
5. Pentru tablă și editor: selectarea contextului, ordinea, gruparea pe luni, metadatele cardurilor (datele), încadrarea cardurilor în celule, drag-and-drop, taburile, minimizarea și avertizarea pentru modificări nesalvate.
6. Pentru schimbări ale fluxului versiunii: citirea din SQL Server și cazul tabelei goale, fără a șterge datele utilizatorului.
7. Procesele `WorkNotes.Web` pornite pentru verificare sunt oprite la final.

Verificările care nu pot fi făcute într-un mediu (de exemplu fără SQL Server într-o sesiune cloud) se raportează explicit ca neefectuate.
