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

PR #4 (neintegrat în `main`) adaugă, pentru referințele interne (rulate pe 2026-09-29, după popup-ul referinței abia scrise: **245** de teste în total):

| Clasa de teste | Acoperă | Teste |
| --- | --- | --- |
| `NoteReferenceRulesTests` | formele `CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `cr 30080`, `Cr-30080`, `bug 30042`…`Bug-30042`; spațiile multiple și neseparabile; limitele (50 de spații, 18 cifre); textele care nu sunt referințe (`XCR30080A`, `debug 1234`, forme cu tab, cu `- ` sau cu cifre de alt alfabet); punctuația din jur; zerourile de la început; ordinea; CR și bug cu același număr; titlurile; notele destinație (toate, fără nota însăși, în ordinea ID-urilor); linkurile fiecărei apariții, cu toate notele și textul lor; referința cu care se termină un text (începutul ei, numai când textul se termină cu ea, cuvânt întreg; numai sfârșitul unui text lung, cu caracterul dinaintea celei mai lungi referințe) | 68 |
| `NoteReferenceParserTests` | tipurile configurate sunt prefixele citite (`TASK`), un tip neconfigurat nu este referință, fără tipuri niciun text nu are referințe, tipurile invalide ignorate (litere mici, cifre, diacritice, alt alfabet, peste 10 litere), un tip care începe alt tip (`CR`, `CRQ`), tipul cel mai lung în textul stocat, numai formele ASCII ale literelor (semnul Kelvin, `ı`, `İ`, `ſ`), pozițiile | 12 |
| `ReferenceTypeServiceTests` | tipurile active sunt prefixele parserului, citite o dată pentru toată aplicația, citite din nou după 5 minute, aceleași până la sfârșitul unei cereri, niciun tip activ, o eroare a bazei (nu devine listă goală, se citește din nou la cererea următoare), anularea și tokenul | 7 |
| `NoteReferenceServiceTests` | stocarea cu textul primei apariții, mai multe referințe într-un paragraf, CR și bug cu același număr, fără destinație, mai multe note cu referința în titlu (toate deschise), nota însăși (niciodată destinație), jurnalul „CRs”, recalcularea după redenumire și creare (o notă adăugată sau scoasă dintre notele referinței), vizibilitatea fiecărui proprietar, reluarea după o destinație ștearsă, linkurile afișate cu toate notele lor, linkurile paragrafelor salvate din rezoluție, tipurile configurate (în paragrafe și în titluri), o referință stocată de un tip dezactivat fără link, anularea; căutarea referinței abia scrise (toate notele ei, fără nota însăși; fără notă; un text care nu se termină cu o referință — un număr, o dată, un cuvânt mai lung, un tip neconfigurat; un text lung; tipurile configurate; anularea și tokenul) | 43 |
| `NoteServiceTests` (17 teste noi, 56 în total) | referințele salvate cu paragrafele, linkurile din răspunsul salvării, luate din rezoluție (și cu mai multe note), recalcularea după titlu și creare, numai după operațiile reușite, nicio recalculare după ștergere, linkurile la deschiderea notei, căutarea referinței numai de proprietar (403 pentru ceilalți, 404 pentru o notă nevăzută), cu contextul notei, și oprirea ei la anulare | 17 |

Tot pe 2026-09-29, pentru tabla care urmează salvările din editor: `NoteRulesTests` are un test nou (previzualizarea din paragrafe întregi, `PreviewOf`, este ce citește tabla), iar `NoteServiceTests` trei (cardul notei salvate, construit din textul salvat, fără citirea tablei; o salvare fără schimbări își păstrează data; o salvare care trece nota în luna curentă aduce ordinea acelei luni) — **249** de teste în total. Testele salvării pornesc acum de la cardul notei (`GetSummaryAsync`), nu de la document.

Tot pe 2026-09-29, pentru schimbarea tipului unei note din editor: `NoteServiceTests` are 7 teste noi (un articol devine jurnalul zilei locale în care a fost creat, un jurnal devine articol fără dată, un jurnal care rămâne jurnal își păstrează data, tipurile necunoscute refuzate) — **256** de teste în total.

Versiunea 0.03, conectarea GitHub (rulate pe 2026-09-29: **320** de teste în total, dintre care 65 noi):

| Clasa de teste | Acoperă | Teste |
| --- | --- | --- |
| `GitAuthorizationRulesTests` | provocarea PKCE (exemplul RFC 7636), `state` și verificatori aleatori, URL-safe, de 43 de caractere; compararea `state` (inclusiv valori lipsă); contul (ID, login, caractere de control, lungimi); scopurile; când se reîmprospătează un token (un minut înainte de expirare, niciodată fără expirare) și când se poate; tokenurile lăsate afară din `ToString()` | 28 |
| `GitHubConnectionServiceTests` | autorizarea (state, provocarea verificatorului, adresa de callback; fără configurare); callback-ul (schimbul cu verificatorul și salvarea contului; alt `state`, `state` lipsă sau cookie lipsă, refuzate înainte de GitHub, chiar și cu o eroare; `access_denied` și alte erori; fără cod; cod refuzat sau GitHub indisponibil; cont refuzat sau prea lung; neconfigurat); verificarea (login-ul redenumit; reîmprospătarea salvată înainte de verificare; fără refresh token; refresh refuzat; refresh făcut între timp de altă cerere; GitHub indisponibil; token refuzat; tokenuri nedecriptabile; fără conexiune; conexiune ștearsă între timp); deconectarea (ștergerea și revocarea; revocarea eșuată; tokenuri nedecriptabile; fără conexiune); anularea, tokenul cererii și erorile bazei | 37 |

Tot pe 2026-09-29, pentru importul repository-urilor (**357** de teste în total, 37 noi; obținerea tokenului, mutată în `GitHubTokenService`, rămâne acoperită de testele verificării din `GitHubConnectionServiceTests`):

| Clasa de teste | Acoperă | Teste |
| --- | --- | --- |
| `GitRepositoryRulesTests` | ID-ul (numai cifre ASCII, lungimea); numele tăiat de spații; repository-urile care nu se pot păstra (fără ID sau nume, caractere de control, adresă care nu este `https` absolută, lungimi); descrierea fără caractere de control, tăiată la coloană; branch-ul prea lung | 17 |
| `GitRepositoryServiceTests` | lista GitHub cu bifa celor importate, în ordinea numelui; un repository importat pe care GitHub nu-l mai arată; normalizarea și duplicatele; lista trunchiată; fără token (neconfigurat, neconectat, reconectare, indisponibil) sau fără răspuns de la GitHub, numai cele importate; salvarea cu datele de la GitHub; un repository dispărut păstrat numai cât rămâne bifat; ID-uri necunoscute, invalide sau repetate ignorate; nimic salvat fără token sau fără listă; conflictul; anularea, tokenul cererii și erorile bazei | 20 |

Clientul HTTP din `WorkNotes.Integrations` nu are teste automate (proiectul de teste referă numai Business). A fost verificat pe 2026-09-29 cu un program temporar, cu un handler HTTP simulat: adresa de autorizare, lista repository-urilor (paginile după `Link: rel="next"`, limita de 1000, 401 și 502), cererile de token și refresh (formular, `Accept: application/json`), `GET /user` (Bearer, `User-Agent`, `X-GitHub-Api-Version`), revocarea (Basic, corp JSON), răspunsurile cu `error`, 401, 503, un corp HTML și o eroare de rețea, anularea.

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
- PR #4, referințele interne: toate formele cerute, termenii întregi, CR și bug distincte, toate notele cu referința în titlu (nota însăși niciodată; fără legătură când nu există nicio altă notă), o singură relație pe paragraf și referință, cu notele ei și cu ID-ul ei din catalog (fiecare referință o singură dată în `WorkReferences`), tipurile configurate în `ReferenceTypes` (un tip nou recunoscut, unul dezactivat nu), recalcularea la schimbarea textului, a titlului unei destinații, la ștergerea unui paragraf și a unei destinații, clicul pe un link al unei note deschise deja într-un tab și deschiderea tuturor notelor unui link, sertarul referințelor (fiecare referință o singură dată, cu toate notele ei, și lista după salvare), popup-ul referinței abia scrise (referința găsită, cu notele ei, sau inexistentă, numai pentru proprietar; nimic pentru un text care nu este referință);
- PR #4, editorul: selecția textului vizibilă pe rândul activ, în titlu și pe ambele foi (jurnal și articol);
- PR #4, tipul notei din editor: numai proprietarul îl schimbă, cu salvarea notei; o notă devenită jurnal are ziua creării, un articol nicio dată; foaia, tabul și cardul urmează tipul;
- PR #4, tabla din spatele editorului: cardul urmează fiecare salvare (titlul și previzualizarea numai când se schimbă, data mereu), o notă dintr-o lună anterioară trece în luna curentă la locul ei, iar închiderea fără modificări nesalvate nu reîncarcă pagina (cu modificări nesalvate avertizează și încarcă tabla; la fel după o salvare pe care tabla nu a putut-o arăta);
- versiunea 0.03, conectarea GitHub: callback-ul numai cu `state`-ul conectării pornite de același utilizator; tokenurile criptate, niciodată afișate; reîmprospătarea unui token care expiră; deconectarea șterge conexiunea și când GitHub nu revocă autorizarea; importul salvează numai repository-uri pe care GitHub le arată utilizatorului (sau importate deja), iar un repository debifat este scos;
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
8. PR #4, pentru sertarul referințelor și selecția din editor: în jurnalul „CRs” și într-un articol, sertarul închis (banda cu numărul referințelor) și deschis, lângă text; fiecare referință o singură dată, cu toate notele ei; click pe o referință (toate notele în taburi) și pe o notă (numai ea), cu modificările nesalvate păstrate; starea deschis/închis în toate taburile; lista după o salvare care adaugă și scoate referințe; Enter și Space pe antet; fără JavaScript; la 320px, sub text. Selecția unui cuvânt (dublu-click, Shift+săgeți, Ctrl+A) pe rândul activ și pe alte rânduri, în titlu, pe ambele foi, și în modul forced-colors (Windows, contrast ridicat). Popup-ul referinței abia scrise: după `CR 30080` și spațiu (și Tab, punct, virgulă, Enter), notele ei și butonul, care o face link imediat, și Tab; o referință fără notă, cu mesajul închis după circa 4 secunde; o dată sau o cantitate, fără popup; Escape (editorul rămâne deschis); închiderea la alt rând și la click în text; salvarea după buton; la 320px și în forced-colors.
9. PR #4, pentru tabla din spatele editorului: salvarea titlului, a primului paragraf și a unui paragraf de după primele trei (cardul, văzut cu editorul minimizat); prima salvare a unei note dintr-o lună anterioară (cardul trece în luna curentă, la locul lui, iar luna rămasă goală dispare); închiderea cu Închide, Escape, click în afară, × pe ultimul tab și din forma minimizată (fără reîncărcare, focusul pe card), Back după ea, apoi deschiderea altei note; închiderea cu modificări nesalvate (avertizarea, apoi tabla încărcată); pe SQL Server, durata închiderii pe o tablă mare.
10. PR #4, pentru tipul notei din editor: jurnal → articol și înapoi (foaia, tabul, forma minimizată, cardul de pe tablă după salvare), cu mouse-ul și cu săgețile; revenirea la tipul salvat; închiderea cu tipul schimbat nesalvat; două taburi cu tipuri diferite; nota unui coleg (fără comutator); fără JavaScript; la 320px și în forced-colors; pe SQL Server, `NoteType` și `JournalDate` după salvare (jurnalul cu ziua creării, articolul cu `NULL`).
11. Versiunea 0.03, conectarea GitHub: `001_CreateGitConnections.sql` aplicat (cu acordul utilizatorului), scaffolding-ul cu `--table dbo.GitConnections` (codul generat trebuie să coincidă cu `Entities/GitConnection.cs` și maparea scrisă manual), o aplicație GitHub de test cu URL-ul de callback local și `GitHub:ClientId` / `GitHub:ClientSecret` în User Secrets. Pagina fără configurare; Conectează GitHub (acordul pe GitHub, întoarcerea, contul afișat), refuzul pe GitHub, un callback deschis din nou sau după 10 minute (mesajul de conectare neconfirmată); Verifică conexiunea (și după expirarea tokenului unei GitHub App, după 8 ore, sau cu `AccessTokenExpiresAtUtc` mutat în trecut în baza de test); Conectează din nou; Deconectează (autorizarea dispare din GitHub → Settings → Applications) și deconectarea după ce autorizarea a fost revocată pe GitHub; în SQL, `ProtectedAccessToken` nu conține tokenul în clar; fără JavaScript; în cele trei limbi; la 320px.
12. Versiunea 0.03, importul repository-urilor: `002_CreateGitRepositories.sql` aplicat și scaffolding-ul cu `--table dbo.GitRepositories`; aplicația GitHub instalată pe cont și pe o organizație. `/Repositories` fără conexiune (mesajul și linkul), apoi conectat: toate repository-urile (proprii, de colaborator, ale organizației), cu vizibilitatea, branch-ul și descrierea; bifarea și salvarea (rândurile în `GitRepositories`), debifarea (rândul șters), un repository redenumit pe GitHub (numele nou după salvare), unul la care accesul a fost pierdut (marcat, păstrat cât rămâne bifat), o selecție salvată din două ferestre; un cont cu peste 100 de repository-uri; fără JavaScript; cu tastatura (bifele au nume pentru cititoarele de ecran); în cele trei limbi; la 320px.
13. Procesele `WorkNotes.Web` pornite pentru verificare sunt oprite la final.

Verificările care nu pot fi făcute într-un mediu (de exemplu fără SQL Server într-o sesiune cloud) se raportează explicit ca neefectuate.
