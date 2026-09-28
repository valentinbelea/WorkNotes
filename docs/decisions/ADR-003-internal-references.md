# ADR-003: Referințele interne între note

- **Status:** Accepted — cerută explicit de utilizator pe 2026-09-28 și implementată în PR #4 (branch `main_task_02`), neintegrat în `main`. Înlocuiește primul model de referințe din PR #4 (legătura păstrată în text, deciziile 33–38 și 40–47 din [jurnal](README.md#jurnalul-deciziilor)), care nu avusese un ADR. Modificată în aceeași zi, la a doua cerere a utilizatorului: o referință deschide toate notele care au CR-ul sau bugul în titlu, nu numai una (deciziile 59–64).
- **Data:** 2026-09-28.
- **Surse:** cerința utilizatorului din 2026-09-28 („Corectăm implementarea sistemului de referințe interne”) și cererea următoare („Mutăm atunci TargetNoteId într-o altă tabelă care să permită legătura 1–M…”); deciziile 39, 51 și 53–64 din jurnal; codul din `NoteReferenceRules`, `NoteReferenceService`, `NoteService`, `NoteRepository`, `note-references.js`, `note-editor.js` și scripturile `Scripts/version_0.02/004_ReplaceNoteReferences.sql` și `005_CreateNoteReferenceTargets.sql`.

## Context

Jurnalul „CRs” și celelalte note scriu CR-uri și buguri (`CR 30080`, `bug-1234`), iar articolul care explică un CR are de obicei CR-ul în titlu („CR 30080 Export facturi”). Primul model din PR #4 lega orice număr de 3–18 cifre aflat în titlul altei note: utilizatorul alegea nota dintr-o sugestie, iar legătura rămânea în textul paragrafului ca `[[note:{id}|{număr}]]`; tabela `NoteReferences` lega nota sursă de nota destinație. Utilizatorul a cerut un alt model: recunoașterea formelor CR/bug, destinația aflată din titluri, relația la nivel de paragraf, textul neschimbat, recalcularea automată la fiecare schimbare, afișarea ca link și reindexarea conținutului existent, cu raportarea referințelor fără destinație și a celor ambigue.

Varianta următoare a legat o referință numai când exact o notă o avea în titlu. În jurnalul „CRs”, `CR 27881` a rămas text, deși exista articolul „CR_27881”, pentru că mai multe note aveau CR-ul în titlu. Utilizatorul a cerut ca nota destinație să treacă într-o tabelă separată, legată 1–M de `NoteReferences`, iar referința să deschidă, la click, toate notele identificate după titlu.

## Decizie

1. **Forma:** o referință este tipul `CR` sau `BUG` (în orice combinație de litere mari și mici, numai litere ASCII), apoi 1–50 de spații (inclusiv spațiul neseparabil), un `-`, un `_` sau nimic, apoi 1–18 cifre ASCII. Tipul și numărul sunt cuvinte întregi: o literă, o cifră sau un semn diacritic combinat lipit înainte de tip ori după număr face textul parte dintr-un cuvânt mai lung (`XCR30080A` nu este referință). Forma normalizată este tipul și numărul fără zerouri la început, `CR:30080` / `BUG:1234`; textul original se păstrează. Regulile sunt în `NoteReferenceRules` (decizia #51).
2. **Destinațiile:** titlurile se citesc cu aceleași reguli. O referință dintr-un paragraf deschide toate notele contextului care au aceeași formă normalizată în titlu, dintre cele pe care proprietarul notei paragrafului le poate vedea, nearhivate, în afară de nota paragrafului: una, două sau mai multe, în ordinea ID-urilor. Fără nicio altă notă, textul rămâne text; `005_CreateNoteReferenceTargets.sql` listează aceste referințe (decizia #59).
3. **Stocarea:** `NoteReferences` are câte un rând pentru fiecare paragraf și formă normalizată cu cel puțin o notă: `NoteBlockId` (cheie externă cu cascadă), `ReferenceType`, `ReferenceNumber`, `ReferenceText` (prima apariție), `NormalizedReference`, `CreatedAtUtc`, cu indexul unic pe `NoteBlockId` + `NormalizedReference` și indexul pe `NormalizedReference`. Notele pe care le deschide sunt în `NoteReferenceTargets`, câte un rând pe notă: `NoteReferenceId` (cheie externă cu cascadă), `TargetNoteId` (fără cascadă, deciziile #39 și #61), `CreatedAtUtc`, cu cheia primară pe cele două ID-uri și indexul pe `TargetNoteId`. Textul paragrafului nu se modifică niciodată pentru a conține legătura (deciziile #53, #60).
4. **Actualizarea:** salvarea unei note scrie referințele tuturor paragrafelor ei, cu notele lor, în aceeași tranzacție cu paragrafele, sub verificarea `RowVersion`. Ce nu mai este valabil se șterge (inclusiv referințele paragrafelor șterse și notele pe care o referință nu le mai deschide), ce lipsește se adaugă, iar ce rămâne valabil se păstrează, cu data lui. Schimbarea titlului unei note (din editor sau de pe card) și crearea unei note cu titlu recalculează, în contextul ei, referințele pentru formele normalizate pe care titlul le-a câștigat sau le-a pierdut. Recalcularea este o tranzacție separată, după ce operația s-a salvat, și atinge numai paragrafele nemodificate de la citire. Ștergerea unei note scoate, în tranzacția ei, rândurile care o deschid și referințele pentru care era singura notă; nu mai este nevoie de o recalculare (deciziile #54, #62).
5. **Serviciul:** `INoteReferenceService` (Business) găsește și rezolvă referințele (`ResolveAsync`), le recalculează după titluri (`RefreshAsync`) și calculează unde se afișează linkurile și ce note deschide fiecare (`WithLinksAsync`); `NoteService` îl apelează la fiecare operație care le poate schimba. Accesul la date este `INoteReferenceRepository`, implementat de `NoteRepository`. Web și JavaScript nu citesc referințe din text.
6. **Afișarea:** serverul trimite, pentru fiecare paragraf, pozițiile linkurilor și notele fiecăruia; editorul le desenează peste text (`cm-note-reference`), cu tooltipul format din titlul și tipul fiecărei note, câte una pe rând. Un click pe link sau Ctrl+Enter cu cursorul pe el deschide toate notele lui, fiecare într-un tab nou sau în tabul ei deja deschis, apoi arată prima. Editorul minimizat revine, iar celelalte taburi își păstrează modificările nesalvate. Textul scris în interiorul unui link sau lângă el îl elimină până la salvare; răspunsul salvării aduce linkurile textului salvat. Fără JavaScript, paragrafele au aceleași linkuri, ca HTML codificat: referința este link către prima notă, iar fiecare notă următoare este un link numerotat mic (2, 3…) după ea. Previzualizarea cardurilor este text simplu (deciziile #55, #56, #63).
7. **Tranziția:** `004_ReplaceNoteReferences.sql` transformă legăturile vechi `[[note:{id}|{număr}]]` înapoi în numărul pe care îl afișau (fără să schimbe auditul). Apoi șterge tabela veche, creează `NoteReferences` cu o singură notă pe rând și citește toate paragrafele care pot avea referințe, inclusiv jurnalul „CRs”, cu aceleași reguli în T-SQL. `002` și `003` rămân nemodificate (au fost aplicate) și nu se mai rulează după `004` (deciziile #57, #58).

   `005_CreateNoteReferenceTargets.sql` creează `NoteReferenceTargets`, mută în ea nota fiecărui rând din `004` (cu data lui) și elimină coloana `NoteReferences.TargetNoteId`. Apoi reindexează conținutul cu regula de la punctul 2 și afișează rezumatul, referințele fără notă, cele cu mai multe note (cu fiecare notă) și jurnalul „CRs”. Cu `@Save = 0` nu salvează nimic. `004` nu se mai rulează după `005`, care îi ia locul pentru rulările ulterioare (decizia #64).

## Motive

- Forma normalizată face din `CR-30080`, `cr 30080` și `CR_30080` aceeași referință și păstrează distincte `CR 1234` și `bug 1234`, cum a cerut utilizatorul.
- Titlul este locul în care utilizatorul numește deja CR-ul unui articol; destinațiile se află fără alegere manuală, iar o redenumire mută legăturile singură.
- Când mai multe note au aceeași referință în titlu, se deschid toate: nu se alege automat una dintre ele, iar utilizatorul trece de la una la alta prin taburi, cum a cerut.
- Nota însăși nu se deschide niciodată: două note cu `CR 30080` în titlu se deschid una pe cealaltă, iar restul contextului le deschide pe amândouă.
- Relația pe paragraf este cerința utilizatorului: ștergerea unui paragraf îi ia legăturile (cascadă), iar o referință repetată în paragraf are un singur rând. Tabela separată pentru note ține o referință cu mai multe note într-un singur rând al paragrafului.
- Textul neschimbat se copiază, se caută și se anulează ca orice text; nu există marcaj care să apară sau să se strice la copiere.
- Vizibilitatea proprietarului paragrafului: el scrie textul; legăturile nu dezvăluie notele private ale colegilor, iar un cititor vede numai notele pe care le poate vedea.
- Recalcularea numai pentru formele câștigate sau pierdute de titlu citește doar paragrafele care conțin cifrele lor.
- Pozițiile calculate pe server păstrează regulile numai în Business ([AGENTS.md](../../AGENTS.md#arhitectură-și-dependențe)).
- Reindexarea prin script SQL, cum aplică utilizatorul toate schimbările bazei. Recunoașterea din scripturi a fost comparată cu `NoteReferenceRules` pe 400 000 de texte generate aleatoriu, fără nicio diferență. Regula notelor din `005` a fost comparată cu `NoteReferenceService` pe 3000 de table generate aleatoriu, tot fără nicio diferență.

## Alternative cunoscute

- **Legătura păstrată în text (primul model din PR #4):** înlocuită la cererea utilizatorului.
- **Nicio legătură când mai multe note au referința în titlu (varianta inițială a acestui ADR, decizia #52):** înlocuită la cererea utilizatorului, după cazul `CR 27881` din jurnalul „CRs”.
- **Recunoașterea în JavaScript, în timpul scrierii:** respinsă — regulile ar exista de două ori, iar JavaScript nu conține reguli de business; linkurile unei referințe noi apar la salvare.
- **Alegerea articolului dintre mai multe note:** propusă și retrasă anterior (commit-ul `d82dcd0`, anulat de `1327208` la cererea utilizatorului); nu se alege automat.
- **Pozițiile referințelor stocate în bază (ancore):** respinse — pozițiile rezultă din text la fiecare afișare, iar în editor se mută odată cu textul până la salvare.
- **Linkuri în previzualizarea cardurilor:** renunțat — cardul deschide deja nota, iar tabla nu citește referințe.
- **O singură tranzacție pentru schimbarea titlului și recalculare:** ar cere o unitate de lucru între servicii și repository-uri, pe care [AGENTS.md](../../AGENTS.md#solid-interfețe-și-dependency-injection) nu o permite fără necesitate demonstrabilă; recalcularea este o tranzacție separată, cu limitările de mai jos.
- **Reindexarea printr-o comandă a aplicației** (`create-note-references`, eliminată): utilizatorul aplică schimbările bazei prin scripturi SQL.

## Consecințe

- O referință nou scrisă devine link după salvare; textul scris într-un link îl ascunde până la salvare; Ctrl+Z nu readuce un link înainte de salvare.
- O referință cu multe note deschide tot atâtea taburi.
- Fără JavaScript, un link deschide o singură notă: celelalte au câte un link numerotat.
- Referințele fără notă nu au semn în editor; o nouă rulare a `005_CreateNoteReferenceTargets.sql` le listează.
- Două operații simultane pe aceeași referință, în același context, sau o cerere întreruptă între operație și recalculare pot lăsa o legătură învechită. Ea rămâne până la următoarea salvare a paragrafului sau a titlului ori până la o nouă rulare a `005` ([DATABASE.md](../DATABASE.md#concurență)).
- Schimbarea vizibilității, arhivarea și ieșirea unui membru din context nu recalculează legăturile (nu au interfață); următoarea salvare sau o nouă rulare a `005` le aduce la zi.
- Scripturile recunosc literele fără majuscule numai pentru alfabetul latin; într-un text în alt alfabet, o referință lipită de o literă poate fi legată de script, iar aplicația o corectează la următoarea salvare a paragrafului.
- Numerele legate în primul model fără `CR` sau `BUG` înainte redevin text simplu.
- `002_CreateNoteReferences.sql` și `003_InsertNoteReferences.sql` nu mai pot fi rulate după `004`, iar `004` nu mai poate fi rulat după `005`.
- Codul presupune că `005` a fost aplicat: fără `NoteReferenceTargets`, deschiderea și salvarea notelor dau eroare.
