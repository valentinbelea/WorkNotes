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
| Referință internă între note | în dezvoltare (PR #4) | `NoteReferenceRules`, `NoteReferenceTypes`, `NoteReferenceMatch`, `NoteBlockReference`, `INoteReferenceService` (PR #4) | `NoteReferences` (PR #4) |
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
  - titlul este opțional (captura nu se blochează), cel mult 200 de caractere, fără caractere de control, normalizat prin `Trim`; un titlu gol înseamnă „Fără titlu”;
  - o notă `Private` este văzută numai de proprietar; o notă `Context` este văzută de membrii contextului, numai pentru citire; numai proprietarul o editează, o redenumește, o șterge și îi schimbă locul;
  - ultima modificare este `ISNULL(ModifiedAtUtc, CreatedAtUtc)` (`NoteSummary.LastChangedAtUtc`); o notă nemodificată de la creare nu are dată de modificare;
  - pe tablă, notele sunt grupate după luna locală a ultimei modificări, cele mai noi luni primele; în fiecare lună ordinea este `Order` crescător, apoi ultima modificare, crearea și `Id`, descrescător;
  - o notă nouă primește ordinea minimă a contextului minus 1, deci apare prima în luna curentă; editarea nu schimbă ordinea, iar o notă modificată trece în luna modificării;
  - proprietarul poate schimba locurile a două note ale sale din același context și din aceeași lună locală; schimbul nu modifică auditul, deci nicio notă nu își schimbă luna;
  - o notă arhivată (`ArchivedAtUtc` completat) nu este vizibilă nicăieri; arhivarea nu are încă interfață;
  - ștergerea elimină nota împreună cu paragrafele ei.
- Statusuri: `NoteCreateStatus` (`Created`, `InvalidType`, `InvalidTitle`, `ContextNotFound`), `NoteSaveStatus` (`Saved`, `NotFound`, `Forbidden`, `Conflict`, `InvalidTitle`, `InvalidContent`), `NoteDeleteStatus` (`Deleted`, `NotFound`, `Forbidden`), `NoteOrderStatus` (`Saved`, `NotFound`, `Forbidden`, `InvalidTarget`, `Conflict`).

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

**Referința internă între note** (PR #4, branch `main_task_02`, neintegrat în `main`). Regulile sunt în `NoteReferenceRules` și `NoteReferenceService`; decizia este [ADR-003](decisions/ADR-003-internal-references.md).

- Scop: un CR sau un bug scris într-un paragraf deschide nota care îl are în titlu — de exemplu `CR 30080` din jurnalul „CRs” deschide articolul „CR 30080 Export facturi”.
- Formele recunoscute: tipul `CR` sau `BUG`, cu orice combinație de litere mari și mici, apoi unul până la 50 de spații (sau spații neseparabile), un `-`, un `_` ori nimic, apoi un număr de 1–18 cifre ASCII: `CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `cr 30080`, `Cr-30080`, `bug 1234`, `bug-1234`, `bug_1234`, `bug1234`, `BUG 1234`, `Bug-1234`. Tipul și numărul sunt cuvinte întregi: o literă, o cifră sau un semn diacritic combinat lipit înainte de tip ori după număr le face parte dintr-un cuvânt mai lung (`XCR30080A` nu este referință); punctuația, `/` și `_` nu (`feature/CR-30080_export` conține `CR-30080`).
- Normalizarea: tipul (`CR`, `BUG`, constantele `NoteReferenceTypes`), numărul (fără zerourile de la început) și forma normalizată `CR:30080` / `BUG:1234`, după care se compară referințele; textul original (`CR_30080`) se păstrează. `CR 1234` și `bug 1234` sunt referințe diferite.
- Nota destinație: titlurile notelor se citesc cu aceleași reguli. O referință dintr-un paragraf deschide nota atunci când exact o notă a contextului — dintre cele pe care proprietarul notei paragrafului le poate vedea (ale lui sau partajate cu contextul), nearhivate, nota însăși inclusă — are referința în titlu, iar aceasta este altă notă. Fără nicio notă referința rămâne text (fără destinație); cu mai multe note nu se alege automat (ambiguă); dacă numai nota însăși o are, rămâne text. Contextul rămâne granița de acces.
- Stocarea: câte un rând în `NoteReferences` pentru fiecare paragraf și referință normalizată cu destinație, oricâte apariții ar avea referința în paragraf; rândul păstrează textul primei apariții. Textul paragrafului nu se modifică niciodată pentru a conține linkul.
- Actualizarea: salvarea unei note scrie referințele paragrafelor ei în aceeași tranzacție cu paragrafele (cele ale paragrafelor șterse dispar, cele noi se adaugă, cele valabile rămân cu data creării); schimbarea titlului unei note, crearea unei note cu titlu și ștergerea unei note recalculează, în context, referințele pentru formele normalizate pe care titlul le-a câștigat sau le-a pierdut. Ștergerea notei destinație elimină rândurile care o deschideau; paragrafele își păstrează textul.
- Afișarea: fiecare loc din paragraf în care este scrisă o referință stocată, oricare i-ar fi forma, este un link către nota ei, pentru cititorii care pot vedea nota destinație; pozițiile se calculează din text la fiecare afișare, nu se stochează.

## Entități planificate

Din planul inițial; nu au cod, tabele sau interfață:

| Entitate / tabelă | Rol |
| --- | --- |
| Referință de lucru (`WorkReferences`) | Catalogul de CR-uri și buguri dintr-un sistem extern, pe context: `Id`, `ContextId`, `ReferenceType` (`CR`, `Bug`), `Code` (text), `Title` și `ExternalUrl` opționale, audit; unic pe `ContextId + ReferenceType + Code`. URL-urile nu se deduc din cod. |
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
