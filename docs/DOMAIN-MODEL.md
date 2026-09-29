# Modelul de domeniu

Entitățile WorkNotes, cu regulile de business implementate în `WorkNotes.Business` și tabelele din [DATABASE.md](DATABASE.md). Terminologia este în [PROJECT-CONTEXT.md](PROJECT-CONTEXT.md#terminologie).

## Privire de ansamblu

```text
Utilizator ──membru (Owner / Member)──► Context de lucru ──conține──► Notă (jurnal / articol) ──conține──► Paragraf
                                                                        │
                                              (planificat) Referință de lucru CR/Bug, Legătură externă
```

| Entitate | Stare | Business | Tabelă |
| --- | --- | --- | --- |
| Utilizator | implementată | `AccountProfile`, `AccountRules` | `Users` (+ tabelele Identity) |
| Context de lucru | implementată | `WorkContext`, `WorkContextRules` | `WorkContexts` |
| Membru al contextului | implementată | `ContextMemberDetails`, `ContextRoles` | `ContextMembers` |
| Notă | implementată | `NoteSummary`, `NoteDocument`, `NewNote`, `NoteRules`, `NoteTypes`, `NoteVisibilities` | `Notes` |
| Paragraf (bloc) | implementată | `NoteBlockDetails`, `NoteBlockInput`, `NoteBlockAudit` | `NoteBlocks` |
| Versiunea bazei | implementată | `ApplicationVersionService` | `DatabaseVersion` |
| Referință internă între note | în dezvoltare (PR #4) | `NoteReferenceRules`, `NoteReferenceParser`, `NoteReferenceMatch`, `NoteBlockReference`, `INoteReferenceService`, `IReferenceTypeService` (PR #4) | `NoteReferences`, `NoteReferenceTargets`, `WorkReferences`, `ReferenceTypes` (PR #4) |
| Referință de lucru, legătură, platformă și modulele ulterioare | planificate | — | — |

Tabla nu este o entitate: este afișarea notelor unui context, grupate pe luni.

## Entități implementate

### Utilizator

- Scop: contul unei persoane (ASP.NET Core Identity, `ApplicationUser`).
- Proprietăți: `Id`, `FirstName`, `LastName`, `Email` (unic după normalizarea Identity; `UserName` = e-mailul).
- Reguli: prenumele și numele sunt obligatorii, cel mult 100 de caractere, fără caractere de control, normalizate prin `Trim`; e-mailul are cel mult 256 de caractere, este valid și nu se editează; politica de parolă și blocarea sunt în [AGENTS.md](../AGENTS.md#identitate-și-autentificare).
- Rezultate: `AccountResult` (`Succeeded`, `Errors` = coduri/chei de mesaj).

### Context de lucru

- Scop: spațiul de lucru în care stau notele — un client sau o firmă (de exemplu SD Worx, TopDev). Orice notă aparține unui singur context. Contextul este granița de acces.
- Proprietăți: `Id`, `Name` (obligatoriu, cel mult 100 de caractere, fără caractere de control), `Description` (opțională, cel mult 1000 de caractere, poate avea mai multe rânduri); modelul Business adaugă `IsOwner`, rolul utilizatorului curent.
- Relații: are membri (cel puțin proprietarul) și note.
- Reguli:
  - un utilizator vede numai contextele în care este membru, cu orice rol; pentru celelalte, contextul „nu există”;
  - creatorul devine membru `Owner` în aceeași tranzacție cu contextul;
  - numai `Owner` editează, șterge și gestionează membrii;
  - numele este unic global, deci și un nume folosit într-un context străin este refuzat ca duplicat;
  - numele și descrierea sunt normalizate prin `Trim`; o descriere goală devine `NULL`;
  - un context care are note nu poate fi șters.
- Statusuri: `WorkContextSaveStatus` (`Saved`, `NotFound`, `Forbidden`, `DuplicateName`, `InvalidName`, `InvalidDescription`), `WorkContextDeleteStatus` (`Deleted`, `NotFound`, `Forbidden`, `InUse`).

### Membru al contextului

- Scop: accesul unui utilizator la un context, cu un rol.
- Proprietăți: `ContextId`, `UserId`, `Role` (`Owner` / `Member`), `AddedAtUtc`; modelul `ContextMemberDetails` adaugă numele și e-mailul contului.
- Reguli:
  - proprietarul adaugă un membru după e-mail (căutat după e-mailul normalizat de Identity); membrul adăugat primește rolul `Member`; proprietatea nu se transferă;
  - numai membrii cu rolul `Member` pot fi eliminați; proprietarul nu se poate elimina, deci un context își păstrează proprietarul;
  - membrul eliminat pierde imediat accesul la context;
  - lista membrilor (vizibilă numai proprietarului) arată întâi proprietarul, apoi membrii în ordinea adăugării.
- Statusuri: `ContextMemberAddStatus` (`Added`, `NotFound`, `Forbidden`, `InvalidEmail`, `UserNotFound`, `AlreadyMember`), `ContextMemberRemoveStatus` (`Removed`, `NotFound`, `Forbidden`, `MemberNotFound`).

### Notă

- Scop: unitatea de lucru, de tip **jurnal** (`Journal`, legat de o zi) sau **articol** (`Article`, fișă tematică, fără dată). Planul inițial numea articolul „Document”.
- Proprietăți: `Id`, `ContextId`, proprietarul, `NoteType`, `Title` (opțional), `JournalDate`, `Visibility` (`Private` / `Context`), auditul creării și al ultimei modificări, `ArchivedAtUtc`, `RowVersion` (versiunea, ca token opac), `Order` (locul pe tablă).
- Relații: aparține unui context și unui proprietar; conține paragrafele în ordine.
- Reguli:
  - orice membru al contextului poate crea note în el; o notă nouă este privată;
  - un jurnal este datat cu ziua locală a aplicației în momentul creării; se pot crea oricâte jurnale pe zi; un articol nu are dată;
  - PR #4: proprietarul poate schimba tipul notei în editor, din jurnal în articol și invers, salvat odată cu nota; o notă devenită jurnal primește ziua locală în care a fost creată, un articol nu are dată, iar o notă care își păstrează tipul își păstrează data; schimbarea tipului este o modificare a notei (ultima modificare), ca a titlului;
  - titlul este opțional (captura nu se blochează), cel mult 200 de caractere, fără caractere de control, normalizat prin `Trim`; un titlu gol înseamnă „Fără titlu”;
  - o notă `Private` este văzută numai de proprietar; o notă `Context` este văzută de membrii contextului, numai pentru citire; numai proprietarul o editează, o redenumește, o șterge și îi schimbă locul;
  - ultima modificare este `ISNULL(ModifiedAtUtc, CreatedAtUtc)` (`NoteSummary.LastChangedAtUtc`); o notă nemodificată de la creare nu are dată de modificare;
  - pe tablă, notele sunt grupate după luna locală a ultimei modificări, cele mai noi luni primele; în fiecare lună ordinea este `Order` crescător, apoi ultima modificare, crearea și `Id`, descrescător;
  - o notă nouă primește ordinea minimă a contextului minus 1, deci apare prima în luna curentă; editarea nu schimbă ordinea, iar o notă modificată trece în luna modificării;
  - proprietarul poate schimba locurile a două note ale sale din același context și din aceeași lună locală; schimbul nu modifică auditul, deci nicio notă nu își schimbă luna;
  - o notă arhivată (`ArchivedAtUtc` completat) nu este vizibilă nicăieri; arhivarea nu are încă interfață;
  - ștergerea elimină nota împreună cu paragrafele ei.
- Statusuri: `NoteCreateStatus` (`Created`, `InvalidType`, `InvalidTitle`, `ContextNotFound`), `NoteSaveStatus` (`Saved`, `NotFound`, `Forbidden`, `Conflict`, `InvalidTitle`, `InvalidContent`; PR #4: `InvalidType`), `NoteDeleteStatus` (`Deleted`, `NotFound`, `Forbidden`), `NoteOrderStatus` (`Saved`, `NotFound`, `Forbidden`, `InvalidTarget`, `Conflict`).

### Paragraf (NoteBlock)

- Scop: textul notei, păstrat pe paragrafe, fiecare cu identitate stabilă și audit propriu (creat / modificat), independent de rândurile vizuale.
- Proprietăți: `Id` (GUID generat de editor), `Position`, `Content`, auditul; `ActivityDate` și `IsImportant` sunt pregătite din plan, fără interfață.
- Reguli (confirmate și implementate):
  - un paragraf este textul dintre rânduri goale, nu rândul vizual; redimensionarea ferestrei nu schimbă nimic;
  - editarea păstrează identitatea și data creării; se actualizează numai auditul paragrafelor modificate;
  - mutarea (schimbarea poziției) păstrează identitatea și nu contează ca modificare;
  - împărțirea păstrează ID-ul pe primul fragment, iar fragmentul desprins primește ID nou; unirea păstrează ID-ul primului paragraf;
  - ștergerea urmată de undo readuce ID-ul paragrafului;
  - textul copiat și lipit primește ID nou; mutarea prin tăiere și lipire creează deocamdată tot un paragraf nou;
  - o salvare trimite toată nota: paragrafele păstrate, modificate, noi și eliminate se deduc din comparația cu cele salvate; un ID nou nu poate prelua paragraful altei note;
  - conținutul nu poate fi gol și nu poate avea caractere de control în afară de rând nou și Tab; terminațiile de rând sunt normalizate la `\n`, iar rândurile goale din jurul paragrafului nu fac parte din el;
  - cel mult 5000 de paragrafe și 1 000 000 de caractere pe notă (`NoteRules`);
  - `RowVersion` este mecanism de concurență, nu istoric al textului.
- Previzualizarea cardului: primele 3 paragrafe (câte cel mult 300 de caractere citite), unul pe rând, cel mult 280 de caractere, tăiate la un cuvânt, cu „…”.

### Versiunea bazei de date

- Scop: eticheta versiunii aplicate (`v.0.01`, `v.0.02`), afișată în footer.
- Regula: versiunea curentă este cea mai mare versiune numerică; eticheta se afișează nemodificată ([VERSIONING.md](VERSIONING.md#tabela-databaseversion)).

## Exemplu

Exemplul din planul inițial (elementele marcate „planificat” nu există încă):

```text
Context: SD Worx
 ├─ Referință de lucru: CR 30042                   (planificat)
 ├─ Notă (jurnal): jurnalul unei zile
 │   └─ Paragraf: „Am verificat scripturile…”      → asociat cu CR 30042 (planificat)
 └─ Notă (articol): „CR 30042”
     ├─ Paragraf: analiză
     ├─ Paragraf: SQL pentru versiunea 2.2.13.4
     └─ Paragraf: particularități custom
```

Jurnalul păstrează cronologia, iar articolul adună explicațiile; codul CR-ului leagă cele două.

## În dezvoltare

**Referința internă între note** (PR #4, branch `main_task_02`, neintegrat în `main`). Regulile sunt în `NoteReferenceRules`, `NoteReferenceParser` și `NoteReferenceService`; decizia este [ADR-003](decisions/ADR-003-internal-references.md).

- Scop: un CR sau un bug scris într-un paragraf deschide notele care îl au în titlu — de exemplu `CR 30080` din jurnalul „CRs” deschide articolul „CR 30080 Export facturi”, iar dacă și jurnalul „CR-30080 testare” are CR-ul în titlu, le deschide pe amândouă.
- Tipurile de referință: prefixele recunoscute sunt tipurile active din tabela de configurare `ReferenceTypes`, implicit `CR` și `BUG`; un tip are 1–10 litere ASCII, stocate cu majuscule (de exemplu se poate adăuga `TASK`). Un tip dezactivat nu se mai citește în texte, dar rămâne în tabelă pentru referințele stocate cu el; un tip folosit nu se șterge. Aplicația ține tipurile în memorie, aceleași pentru toți utilizatorii, și le citește din nou după cel mult 5 minute; o schimbare ajunge la textele deja salvate la următoarea lor salvare sau prin `012_RefreshNoteReferences.sql`.
- Formele recunoscute: un tip activ (`CR`, `BUG`…), cu orice combinație de litere mari și mici (numai formele ASCII ale fiecărei litere), apoi unul până la 50 de spații (sau spații neseparabile), un `-`, un `_` ori nimic, apoi un număr de 1–18 cifre ASCII: `CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `cr 30080`, `Cr-30080`, `bug 1234`, `bug-1234`, `bug_1234`, `bug1234`, `BUG 1234`, `Bug-1234`. Tipul și numărul sunt cuvinte întregi: o literă, o cifră sau un semn diacritic combinat lipit înainte de tip ori după număr le face parte dintr-un cuvânt mai lung (`XCR30080A` nu este referință); punctuația, `/` și `_` nu (`feature/CR-30080_export` conține `CR-30080`).
- Normalizarea: tipul, cu majuscule, cum îl are `ReferenceTypes` (`CR`, `BUG`), numărul (fără zerourile de la început) și forma normalizată `CR:30080` / `BUG:1234`, după care se compară referințele; textul original (`CR_30080`) se păstrează. `CR 1234` și `bug 1234` sunt referințe diferite.
- Notele destinație: titlurile notelor se citesc cu aceleași reguli. O referință dintr-un paragraf deschide toate notele contextului care au referința în titlu, dintre cele pe care proprietarul notei paragrafului le poate vedea (ale lui sau partajate cu contextul), nearhivate, în afară de nota paragrafului: una, două sau mai multe, în ordinea ID-urilor. Fără nicio altă notă, referința rămâne text (fără destinație, inclusiv când numai nota însăși o are în titlu). Contextul rămâne granița de acces.
- Stocarea: câte un rând în `NoteReferences` pentru fiecare paragraf și referință normalizată cu cel puțin o notă, oricâte apariții ar avea referința în paragraf; rândul păstrează textul primei apariții. Notele pe care le deschide sunt în `NoteReferenceTargets`, câte un rând pe notă (o legătură 1–M). Textul paragrafului nu se modifică niciodată pentru a conține linkul.
- Catalogul referințelor (`WorkReferences`): fiecare referință stocată apare o singură dată, după tip și număr (cheia unică), cu ID-ul ei; rândul din `NoteReferences` are ID-ul lângă textul referinței (`WorkReferenceId`). O referință intră în catalog prima dată când un paragraf o stochează și rămâne acolo, cu același ID, și după ce niciun paragraf nu o mai scrie. Catalogul este comun tuturor contextelor (conține numai tipul și numărul); referințele fără nicio notă destinație și cele scrise numai în titluri nu intră în el.
- Actualizarea: salvarea unei note scrie referințele paragrafelor ei, cu notele lor, în aceeași tranzacție cu paragrafele (cele ale paragrafelor șterse dispar, cele noi se adaugă, cele valabile rămân cu data creării). Schimbarea titlului unei note și crearea unei note cu titlu recalculează, în context, referințele pentru formele normalizate pe care titlul le-a câștigat sau le-a pierdut: o notă nouă cu `CR 30080` în titlu se adaugă la notele referinței, iar una care pierde CR-ul din titlu dispare dintre ele. Ștergerea unei note destinație elimină rândurile care o deschideau, iar o referință rămasă fără nicio notă dispare; paragrafele își păstrează textul.
- Afișarea: fiecare loc din paragraf în care este scrisă o referință stocată, oricare i-ar fi forma, este un link către notele ei pe care cititorul le poate vedea; un click le deschide pe toate. Pozițiile se calculează din text la fiecare afișare, nu se stochează. Fiecare link poartă tipul și numărul referinței, deci referințele unei note se pot enumera fără să fie citit din nou textul: sertarul din editor arată fiecare referință care este link o singură dată, scrisă ca prima ei apariție, cu toate notele linkurilor ei. O referință abia scrisă, urmată de un spațiu sau de un semn de punctuație, este căutată cu aceleași reguli: popup-ul editorului arată notele ei (și o poate face link înainte de salvare) sau că nu există.

## Entități planificate

Din planul inițial; nu au cod, tabele sau interfață:

| Entitate / tabelă | Rol |
| --- | --- |
| Referință de lucru (`WorkReferences`) | Planul inițial: catalogul de CR-uri și buguri dintr-un sistem extern, pe context (`ContextId`, `Code` text, unic pe `ContextId + ReferenceType + Code`). Din PR #4 catalogul există (`Id`, `ReferenceType`, `ReferenceNumber`, forma normalizată), comun tuturor contextelor, cu cheia unică tip + număr cerută de utilizator; rămân planificate `Title` și `ExternalUrl` opționale și auditul. URL-urile nu se deduc din cod. |
| `NoteWorkReferences` | O notă (de obicei un articol) ↔ una sau mai multe referințe |
| `NoteBlockWorkReferences` | Un paragraf ↔ referințele relevante pentru el |
| Legătură (`NoteLinks`) | URL extern pe notă sau pe paragraf: `NoteId`, `NoteBlockId` opțional, `Url`, `Label` |
| `NotePlatforms` | Nota ↔ platformele la care se referă, când există modulul de platforme |
| Ulterior | `NoteClients`, `NoteProjects`, `NoteBranches`, `NoteEvents`, `NoteReleases`, `NotePublishes`, când există modulele respective |

Reguli deja stabilite pentru ele:

- relațiile se fac prin chei externe explicite, nu printr-o tabelă generică `EntityType + EntityId`;
- toate asocierile respectă contextul notei;
- poziția unei referințe în text (pentru evidențierea în editor) este o problemă separată: ancorele trebuie actualizate la editare, numărul rândului nu este suficient;
- un eveniment real (de exemplu „Publish efectuat”) va avea propria înregistrare în modulul de livrări; simpla lui menționare într-un paragraf nu confirmă nimic.

Întrebările deschise despre aceste entități sunt în [ROADMAP.md](ROADMAP.md#idei-încă-neaprobate).
