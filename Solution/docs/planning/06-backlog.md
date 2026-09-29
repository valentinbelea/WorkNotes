# 06 — Backlog

## Pașii rămași din planul inițial

1. **WorkReferences**: pagina catalogului de CR-uri și buguri, cu titlul și URL-ul extern ale unei referințe; catalogul există din PR #4, comun tuturor contextelor (unic pe tip + număr).
2. **Asocieri**: `NoteWorkReferences` (notă ↔ referințe) și `NoteBlockWorkReferences` (paragraf ↔ referințe); căutarea tuturor explicațiilor după codul CR-ului.
3. **NoteLinks**: legături externe pe notă sau paragraf.
4. **Platforme** și `NotePlatforms`, dacă intră în prima interfață.
5. În editor: evidențierea referințelor în text (ancore actualizate la editare), autocomplete și popup-uri pentru referințe.
6. Ulterior, când există modulele: clienți, proiecte, branch-uri, evenimente, release-uri și publish-uri.
7. Lista „Referințe către această notă” în editor, din `NoteReferenceTargets` (după `TargetNoteId`) și `NoteReferences`, cu sursele pe care cititorul le poate vedea.

## Funcționalități încă neimplementate

- Salvarea automată în editor.
- Schimbarea vizibilității unei note (`Private` / `Context`) din interfață.
- Arhivarea (`ArchivedAtUtc`) și afișarea notelor arhivate.
- Marcajul de paragraf important (`IsImportant`) și data activității (`ActivityDate`).
- Editarea unei note partajate de către colegi (după stabilirea permisiunilor).
- Lista de contexte cu stil propriu pentru opțiunile deschise (acum este `select` nativ, evidențiat de browser).
- Un tip de mesaj „info” separat, dacă apare un mesaj care are nevoie de el.

## Limitări cunoscute

- La reîncărcarea paginii editorului se redeschide doar tabul activ (adresa `/?note={id}`), nu toate taburile.
- Mutarea unui paragraf prin tăiere și lipire creează un paragraf nou (id nou).
- Pe telefon, bara de taburi arată aproximativ un tab și jumătate; restul se derulează.
- Cu fonturi foarte late (de exemplu pe unele sisteme Linux), data modificării de pe card trece pe al doilea rând.
- Redenumirea pe loc nu mută cardul imediat; ordinea se actualizează la următoarea încărcare a tablei.
- După un schimb pe tablă, un editor deschis pe aceeași notă în alt tab sau în altă fereastră primește conflict la următoarea salvare (fără să suprascrie ceva); editorul din aceeași pagină primește noua versiune.
- Ordonarea se face numai cu mouse-ul (drag-and-drop HTML5); nu există încă o alternativă de la tastatură, iar pe ecranele tactile depinde de suportul browserului.
- Două note cu aceeași valoare `Order` (posibil numai prin inserări directe în SQL) nu își schimbă locurile prin drag-and-drop; tabla le ordonează după date.
- Tooltipul unui link (titlul destinației) și linkurile unei note deschise se actualizează la următoarea deschidere sau salvare a notei, nu imediat după redenumirea destinației în alt tab sau în altă fereastră.
- O referință nou scrisă sau lipită devine link abia după salvare; textul scris într-un link sau lângă el îl ascunde până la salvare; Ctrl+Z nu readuce un link înainte de salvare.
- Referințele fără destinație nu au niciun semn în editor; le listează `012_RefreshNoteReferences.sql`, rulat din nou.
- O referință cu multe note deschide tot atâtea taburi; fără JavaScript, linkul deschide numai prima notă, iar celelalte au câte un link numerotat.
- Recalcularea după schimbarea unui titlu, crearea sau ștergerea unei note este o tranzacție separată de operație: două operații simultane pe aceeași referință sau o cerere întreruptă între ele pot lăsa o legătură învechită până la următoarea salvare.
- Schimbarea vizibilității, arhivarea și ieșirea unui membru din context nu recalculează legăturile (nu au încă interfață).
- Previzualizarea cardurilor este text simplu, fără linkuri.
- Într-o notă read-only (a unui coleg), linkurile se deschid cu mouse-ul; de la tastatură nu, pentru că editorul read-only nu primește focus.
- Închiderea editorului nu mai reîncarcă tabla (tabla urmează salvările din editor), deci modificările altor membri apar abia la următoarea încărcare a paginii; redenumirea de pe card nu mută încă cardul în luna curentă.

## Întrebări deschise

- Platformele intră în prima interfață a notelor?
- Cine poate adăuga referințe în catalog: orice membru al contextului sau numai proprietarul?
- Se păstrează un istoric al textului paragrafelor (versiuni), separat de `RowVersion`?
