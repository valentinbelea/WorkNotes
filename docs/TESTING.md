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

PR #4 (neintegrat în `main`) adaugă, pentru referințele interne (rulate pe 2026-09-28, după trecerea la tipurile configurabile: **220** de teste în total):

| Clasa de teste | Acoperă | Teste |
| --- | --- | --- |
| `NoteReferenceRulesTests` | formele `CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `cr 30080`, `Cr-30080`, `bug 30042`…`Bug-30042`; spațiile multiple și neseparabile; limitele (50 de spații, 18 cifre); textele care nu sunt referințe (`XCR30080A`, `debug 1234`, forme cu tab, cu `- ` sau cu cifre de alt alfabet); punctuația din jur; zerourile de la început; ordinea; CR și bug cu același număr; titlurile; notele destinație (toate, fără nota însăși, în ordinea ID-urilor); linkurile fiecărei apariții, cu toate notele | 59 |
| `NoteReferenceParserTests` | tipurile configurate sunt prefixele citite (`TASK`), un tip neconfigurat nu este referință, fără tipuri niciun text nu are referințe, tipurile invalide ignorate (litere mici, cifre, diacritice, alt alfabet, peste 10 litere), un tip care începe alt tip (`CR`, `CRQ`), tipul cel mai lung în textul stocat, numai formele ASCII ale literelor (semnul Kelvin, `ı`, `İ`, `ſ`), pozițiile | 12 |
| `ReferenceTypeServiceTests` | tipurile active sunt prefixele parserului, citite o dată pentru toată aplicația, citite din nou după 5 minute, aceleași până la sfârșitul unei cereri, niciun tip activ, o eroare a bazei (nu devine listă goală, se citește din nou la cererea următoare), anularea și tokenul | 7 |
| `NoteReferenceServiceTests` | stocarea cu textul primei apariții, mai multe referințe într-un paragraf, CR și bug cu același număr, fără destinație, mai multe note cu referința în titlu (toate deschise), nota însăși (niciodată destinație), jurnalul „CRs”, recalcularea după redenumire și creare (o notă adăugată sau scoasă dintre notele referinței), vizibilitatea fiecărui proprietar, reluarea după o destinație ștearsă, linkurile afișate cu toate notele lor, linkurile paragrafelor salvate din rezoluție, tipurile configurate (în paragrafe și în titluri), o referință stocată de un tip dezactivat fără link, anularea | 31 |
| `NoteServiceTests` (13 teste noi, 52 în total) | referințele salvate cu paragrafele, linkurile din răspunsul salvării, luate din rezoluție (și cu mai multe note), recalcularea după titlu și creare, numai după operațiile reușite, nicio recalculare după ștergere, linkurile la deschiderea notei | 13 |

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
- PR #4, referințele interne: toate formele cerute, termenii întregi, CR și bug distincte, toate notele cu referința în titlu (nota însăși niciodată; fără legătură când nu există nicio altă notă), o singură relație pe paragraf și referință, cu notele ei și cu ID-ul ei din catalog (fiecare referință o singură dată în `WorkReferences`), tipurile configurate în `ReferenceTypes` (un tip nou recunoscut, unul dezactivat nu), recalcularea la schimbarea textului, a titlului unei destinații, la ștergerea unui paragraf și a unei destinații, clicul pe un link al unei note deschise deja într-un tab și deschiderea tuturor notelor unui link;
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
7. PR #4, pentru referințele interne: pe o copie a bazei sau cu acordul utilizatorului, `004_ReplaceNoteReferences.sql` (dacă nu a fost aplicat), apoi `005_CreateNoteReferenceTargets.sql`, rulate întâi cu `@Save = 0` (listele lui `005`: rezumatul, referințele fără notă, cele cu mai multe note, jurnalul „CRs”), apoi cu `@Save = 1`; apoi `006_CreateWorkReferences.sql`, `007_InsertWorkReferences.sql` și `008_UpdateNoteReferencesWorkReferenceId.sql` (ultimele două întâi cu `@Save = 0`), apoi `009_CreateReferenceTypes.sql`, `010_InsertReferenceTypes.sql` și `011_UpdateReferenceTypeKeys.sql`, urmate de interogările de verificare din [Scripts/README.md](../Scripts/README.md#verificarea-după-aplicare) (fiecare rând din `NoteReferences` cu referința lui din catalog, catalogul fără duplicate, tipurile `CR` și `BUG`, cheile externe ale tipului). Un tip adăugat în `ReferenceTypes` (de exemplu `TASK`), apoi `012_RefreshNoteReferences.sql`, întâi cu `@Save = 0`: după cel mult 5 minute, aplicația îl recunoaște la salvare, iar `012` leagă textele deja scrise; dezactivat, apoi `012`, linkurile lui dispar. În aplicație: jurnalul „CRs” cu linkurile lui, inclusiv `CR 27881`; click și Ctrl+Enter pe un link cu o notă și pe unul cu mai multe (tab nou, tab deja deschis, editor minimizat); o referință nou scrisă și salvată (una care nu era în catalog apare în `WorkReferences` o singură dată, iar rândul ei din `NoteReferences` are `WorkReferenceId`); redenumirea unei destinații și ștergerea ei.
8. Procesele `WorkNotes.Web` pornite pentru verificare sunt oprite la final.

Verificările care nu pot fi făcute într-un mediu (de exemplu fără SQL Server într-o sesiune cloud) se raportează explicit ca neefectuate.
