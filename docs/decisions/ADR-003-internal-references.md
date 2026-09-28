# ADR-003: Referințele interne între note

- **Status:** Accepted — cerută explicit de utilizator pe 2026-09-28 și implementată în PR #4 (branch `main_task_02`), neintegrat în `main`. Înlocuiește primul model de referințe din PR #4 (legătura păstrată în text, deciziile 33–38 și 40–47 din [jurnal](README.md#jurnalul-deciziilor)), care nu avusese un ADR.
- **Data:** 2026-09-28.
- **Surse:** cerința utilizatorului din 2026-09-28 („Corectăm implementarea sistemului de referințe interne”); deciziile 39 și 51–58 din jurnal; codul din `NoteReferenceRules`, `NoteReferenceService`, `NoteService`, `NoteRepository`, `note-references.js`, `note-editor.js` și `Scripts/version_0.02/004_ReplaceNoteReferences.sql`.

## Context

Jurnalul „CRs” și celelalte note scriu CR-uri și buguri (`CR 30080`, `bug-1234`), iar articolul care explică un CR are de obicei CR-ul în titlu („CR 30080 Export facturi”). Primul model din PR #4 lega orice număr de 3–18 cifre aflat în titlul altei note: utilizatorul alegea nota dintr-o sugestie, iar legătura rămânea în textul paragrafului ca `[[note:{id}|{număr}]]`; tabela `NoteReferences` lega nota sursă de nota destinație. Utilizatorul a cerut un alt model: recunoașterea formelor CR/bug, destinația aflată din titluri, relația la nivel de paragraf, textul neschimbat, recalcularea automată la fiecare schimbare, afișarea ca link și reindexarea conținutului existent, cu raportarea referințelor fără destinație și a celor ambigue.

## Decizie

1. **Forma:** o referință este tipul `CR` sau `BUG` (în orice combinație de litere mari și mici, numai litere ASCII), apoi 1–50 de spații (inclusiv spațiul neseparabil), un `-`, un `_` sau nimic, apoi 1–18 cifre ASCII. Tipul și numărul sunt cuvinte întregi: o literă, o cifră sau un semn diacritic combinat lipit înainte de tip ori după număr face textul parte dintr-un cuvânt mai lung (`XCR30080A` nu este referință). Forma normalizată este tipul și numărul fără zerouri la început, `CR:30080` / `BUG:1234`; textul original se păstrează. Regulile sunt în `NoteReferenceRules` (decizia #51).
2. **Destinația:** titlurile se citesc cu aceleași reguli. O referință dintr-un paragraf deschide nota atunci când exact o notă a contextului — dintre cele pe care proprietarul notei paragrafului le poate vedea, nearhivate, nota însăși inclusă — are aceeași formă normalizată în titlu, iar aceasta este altă notă. Fără nicio notă sau cu mai multe, textul rămâne text; `004_ReplaceNoteReferences.sql` le raportează (decizia #52).
3. **Stocarea:** `NoteReferences` are câte un rând pentru fiecare paragraf și formă normalizată cu destinație: `NoteBlockId` (cheie externă cu cascadă), `TargetNoteId` (fără cascadă, decizia #39), `ReferenceType`, `ReferenceNumber`, `ReferenceText` (prima apariție), `NormalizedReference`, `CreatedAtUtc`, cu indexul unic pe `NoteBlockId` + `NormalizedReference` și indexuri pe `TargetNoteId` și `NormalizedReference`. Textul paragrafului nu se modifică niciodată pentru a conține legătura (decizia #53).
4. **Actualizarea:** salvarea unei note scrie referințele tuturor paragrafelor ei în aceeași tranzacție cu paragrafele, sub verificarea `RowVersion`: cele care nu mai sunt valabile se șterg (și cele ale paragrafelor șterse), cele noi se adaugă, cele valabile rămân. Schimbarea titlului unei note (din editor sau de pe card), crearea unei note cu titlu și ștergerea unei note recalculează, în contextul ei, referințele pentru formele normalizate pe care titlul le-a câștigat sau le-a pierdut, într-o tranzacție separată, după ce operația s-a salvat, numai pentru paragrafele nemodificate de la citire (decizia #54).
5. **Serviciul:** `INoteReferenceService` (Business) găsește și rezolvă referințele (`ResolveAsync`), le recalculează după titluri (`RefreshAsync`) și calculează unde se afișează linkurile (`WithLinksAsync`); `NoteService` îl apelează la fiecare operație care le poate schimba. Accesul la date este `INoteReferenceRepository`, implementat de `NoteRepository`. Web și JavaScript nu citesc referințe din text.
6. **Afișarea:** serverul trimite, pentru fiecare paragraf, pozițiile linkurilor și notele lor; editorul le desenează peste text (`cm-note-reference`), cu titlul și tipul notei ca tooltip. Un click pe link sau Ctrl+Enter cu cursorul pe el deschide nota într-un tab nou sau selectează tabul ei deja deschis, readuce editorul minimizat și păstrează celelalte taburi, cu modificările lor nesalvate. Textul scris în interiorul unui link sau lângă el îl elimină până la salvare; răspunsul salvării aduce linkurile textului salvat. Fără JavaScript, paragrafele au aceleași linkuri, ca HTML codificat. Previzualizarea cardurilor este text simplu (decizia #55, #56).
7. **Tranziția:** `004_ReplaceNoteReferences.sql` transformă legăturile vechi `[[note:{id}|{număr}]]` înapoi în numărul pe care îl afișau (fără să schimbe auditul), șterge tabela veche, creează tabela nouă și citește toate paragrafele care pot avea referințe, inclusiv jurnalul „CRs”, cu aceleași reguli în T-SQL; afișează rezumatul, referințele fără destinație, cele ambigue, jurnalul „CRs” și paragrafele transformate. Cu `@Save = 0` nu salvează nimic. `002` și `003` rămân nemodificate (au fost aplicate) și nu se mai rulează după `004` (deciziile #57, #58).

## Motive

- Forma normalizată face din `CR-30080`, `cr 30080` și `CR_30080` aceeași referință și păstrează distincte `CR 1234` și `bug 1234`, cum a cerut utilizatorul.
- Titlul este locul în care utilizatorul numește deja CR-ul unui articol; destinația se află fără alegere manuală, iar o redenumire mută legăturile singură.
- Între mai multe note cu aceeași referință, o alegere automată ar fi o ghicire; o legătură greșită este mai rea decât niciuna.
- Nota însăși se numără printre notele cu referința în titlu, ca rezultatul să nu depindă de locul în care este scris textul: altfel, dacă două note au `CR 30080` în titlu, fiecare ar trimite la cealaltă, iar restul contextului la niciuna.
- Relația pe paragraf este cerința utilizatorului: ștergerea unui paragraf îi ia legăturile (cascadă), iar o referință repetată în paragraf are un singur rând.
- Textul neschimbat se copiază, se caută și se anulează ca orice text; nu există marcaj care să apară sau să se strice la copiere.
- Vizibilitatea proprietarului paragrafului: el scrie textul; legăturile nu dezvăluie notele private ale colegilor, iar un cititor vede numai linkurile către notele pe care le poate vedea.
- Recalcularea numai pentru formele câștigate sau pierdute de titlu citește doar paragrafele care conțin cifrele lor.
- Pozițiile calculate pe server păstrează regulile numai în Business ([AGENTS.md](../../AGENTS.md#arhitectură-și-dependențe)).
- Reindexarea prin script SQL, cum aplică utilizatorul toate schimbările bazei; logica scriptului a fost comparată cu `NoteReferenceRules` pe 400 000 de texte generate aleatoriu, fără nicio diferență.

## Alternative cunoscute

- **Legătura păstrată în text (primul model din PR #4):** înlocuită la cererea utilizatorului.
- **Recunoașterea în JavaScript, în timpul scrierii:** respinsă — regulile ar exista de două ori, iar JavaScript nu conține reguli de business; linkurile unei referințe noi apar la salvare.
- **Nota însăși exclusă dintre candidați:** respinsă — vezi motivele.
- **Alegerea articolului dintre mai multe note:** propusă și retrasă anterior (commit-ul `d82dcd0`, anulat de `1327208` la cererea utilizatorului); nu se alege automat.
- **Pozițiile referințelor stocate în bază (ancore):** respinse — pozițiile rezultă din text la fiecare afișare, iar în editor se mută odată cu textul până la salvare.
- **Linkuri în previzualizarea cardurilor:** renunțat — cardul deschide deja nota, iar tabla nu citește referințe.
- **O singură tranzacție pentru schimbarea titlului și recalculare:** ar cere o unitate de lucru între servicii și repository-uri, pe care [AGENTS.md](../../AGENTS.md#solid-interfețe-și-dependency-injection) nu o permite fără necesitate demonstrabilă; recalcularea este o tranzacție separată, cu limitările de mai jos.
- **Reindexarea printr-o comandă a aplicației** (`create-note-references`, eliminată): utilizatorul aplică schimbările bazei prin scripturi SQL.

## Consecințe

- O referință nou scrisă devine link după salvare; textul scris într-un link îl ascunde până la salvare; Ctrl+Z nu readuce un link înainte de salvare.
- Referințele ambigue și cele fără destinație nu au semn în editor; o nouă rulare a `004_ReplaceNoteReferences.sql` le listează.
- Două operații simultane pe aceeași referință, în același context, sau o cerere întreruptă între operație și recalculare pot lăsa o legătură învechită până la următoarea salvare a paragrafului sau a titlului ori până la o nouă rulare a `004` ([DATABASE.md](../DATABASE.md#concurență)).
- Schimbarea vizibilității, arhivarea și ieșirea unui membru din context nu recalculează legăturile (nu au interfață); următoarea salvare sau o nouă rulare a `004` le aduce la zi.
- Scriptul recunoaște literele fără majuscule numai pentru alfabetul latin; într-un text în alt alfabet, o referință lipită de o literă poate fi legată de script, iar aplicația o corectează la următoarea salvare a paragrafului.
- Numerele legate în primul model fără `CR` sau `BUG` înainte redevin text simplu.
- `002_CreateNoteReferences.sql` și `003_InsertNoteReferences.sql` nu mai pot fi rulate după `004`.
