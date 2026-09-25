# 06 — Backlog

## Pașii rămași din planul inițial

1. **WorkReferences**: catalogul de CR-uri și buguri pe context (unic pe `ContextId + ReferenceType + Code`), cu pagina lui.
2. **Asocieri**: `NoteWorkReferences` (notă ↔ referințe) și `NoteBlockWorkReferences` (paragraf ↔ referințe); căutarea tuturor explicațiilor după codul CR-ului.
3. **NoteLinks**: legături externe pe notă sau paragraf.
4. **Platforme** și `NotePlatforms`, dacă intră în prima interfață.
5. În editor: evidențierea referințelor în text (ancore actualizate la editare), autocomplete și popup-uri pentru referințe.
6. Ulterior, când există modulele: clienți, proiecte, branch-uri, evenimente, release-uri și publish-uri.

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

## Întrebări deschise

- Platformele intră în prima interfață a notelor?
- Cine poate adăuga referințe în catalog: orice membru al contextului sau numai proprietarul?
- Se păstrează un istoric al textului paragrafelor (versiuni), separat de `RowVersion`?
