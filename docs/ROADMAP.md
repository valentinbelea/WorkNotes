# Roadmap

Ordonarea pașilor rămași din planul inițial și a punctelor deschise. Nimic din acest document nu este aprobat pentru implementare doar prin faptul că apare aici: o funcționalitate se implementează numai la cerere explicită ([AGENTS.md](../AGENTS.md)). Statusul fiecărei cerințe este în [REQUIREMENTS.md](REQUIREMENTS.md).

## Versiunea minimă funcțională

Nu există o definiție aprobată a versiunii minime funcționale. TODO: Necesită clarificare — ce trebuie să conțină aceasta (de exemplu dacă include referințele CR/bug sau importul jurnalelor TXT).

Baza existentă, livrată în version_0.01 și version_0.02:

- conturi (înregistrare, autentificare, contul meu, schimbarea parolei, deconectare) și localizarea ro/en/pl;
- contexte cu membri și roluri (`Owner` / `Member`);
- tabla pe contexte și luni, cu post-it-uri pentru jurnale și articole, create, redenumite, șterse și ordonate pe loc;
- editorul cu paragrafe auditate, căutare, taburi și minimizare;
- mesajele de salvare, versiunea în footer, funcționarea de bază fără JavaScript.

## În lucru

- PR #4 (branch `main_task_02`): schimburi succesive prin drag-and-drop, fără blocare cât timp se salvează un schimb anterior; deschiderea și închiderea notelor fără reîncărcarea tablei, care urmează salvările din editor; schimbarea tipului unei note din editor; referințele interne CR/bug între note, cu destinațiile aflate din titluri (toate notele cu referința în titlu), relații pe paragraf în `NoteReferences`, cu notele fiecărei referințe în `NoteReferenceTargets` și cu ID-ul fiecărei referințe din catalogul `WorkReferences`, tipurile configurabile din `ReferenceTypes` (ținute în memorie), sertarul referințelor din editor, popup-ul referinței abia scrise și reindexarea conținutului existent prin `version_0.02/004_ReplaceNoteReferences.sql`–`012_RefreshNoteReferences.sql` ([ADR-003](decisions/ADR-003-internal-references.md)); corecția selecției din editor.

## Etapele următoare

Pașii rămași din planul inițial, în ordinea lui:

1. **WorkReferences** — catalogul de CR-uri și buguri, cu pagina lui. Catalogul există din PR #4, comun tuturor contextelor (unic pe tip + număr, cerința utilizatorului din 2026-09-28), fără pagină; rămân pagina, titlul și URL-ul extern. TODO: Necesită clarificare — dacă titlul și URL-ul unui CR se păstrează pe context ([SECURITY.md](SECURITY.md)).
2. **Asocieri** — `NoteWorkReferences` (notă ↔ referințe) și `NoteBlockWorkReferences` (paragraf ↔ referințe); căutarea tuturor explicațiilor după codul CR-ului.
3. **NoteLinks** — legături externe pe notă sau pe paragraf.
4. **Platforme** și `NotePlatforms`, dacă intră în prima interfață.
5. **Editor** — evidențierea referințelor în text (ancore actualizate la editare), autocomplete și popup-uri pentru referințe.
6. **Ulterior**, când există modulele: clienți, proiecte, branch-uri, evenimente, release-uri și publish-uri.
7. Lista „Referințe către această notă” în editor, din `NoteReferenceTargets` (indexul pe `TargetNoteId`) și `NoteReferences`, cu paragrafele sursă pe care cititorul le poate vedea (propusă în PR #4; depinde de integrarea lui).
8. Semnalarea în editor a referințelor fără destinație din textul deja scris (acum le listează doar `012_RefreshNoteReferences.sql`). O referință fără notă abia scrisă este semnalată de popup-ul referinței abia scrise (PR #4, 2026-09-29). Referințele cu mai multe note sunt link către toate, din 2026-09-28.

## Îmbunătățiri

Funcționalități încă neimplementate, menționate în documentele existente:

- salvarea automată în editor;
- schimbarea vizibilității unei note (`Private` / `Context`) din interfață;
- arhivarea (`ArchivedAtUtc`) și afișarea notelor arhivate;
- marcajul de paragraf important (`IsImportant`) și data activității (`ActivityDate`);
- editarea unei note partajate de către colegi, după stabilirea permisiunilor;
- lista de contexte cu stil propriu pentru opțiunile deschise (acum este `select` nativ);
- un tip de mesaj „info” separat, dacă apare un mesaj care are nevoie de el.

Limitări cunoscute care pot deveni îmbunătățiri ([CURRENT-STATUS.md](CURRENT-STATUS.md#probleme-cunoscute)):

- redeschiderea tuturor taburilor la reîncărcarea paginii;
- păstrarea identității unui paragraf mutat prin tăiere și lipire;
- reordonarea de la tastatură și pe ecrane tactile;
- afișarea taburilor pe telefon;
- mutarea imediată a cardului după redenumirea de pe card (după o salvare din editor, cardul se mută deja, cu PR #4);
- modificările altor membri pe tabla deschisă, fără reîncărcare (cu PR #4, nici închiderea editorului nu mai reîncarcă tabla).

## Idei încă neaprobate

Întrebări deschise din planificare:

- Platformele intră în prima interfață a notelor?
- Cine poate adăuga referințe în catalog: orice membru al contextului sau numai proprietarul?
- Se păstrează un istoric al textului paragrafelor (versiuni), separat de `RowVersion`?

Propuneri rezultate din analiza documentației din 2026-09-25, neaprobate:

- teste de integrare pentru repository-uri, pe o bază de test separată;
- pagini de eroare localizate pentru 404, 403 și erorile neașteptate;
- antete de securitate (de exemplu Content-Security-Policy) și configurarea Data Protection pentru găzduire;
- protecția branch-ului `main` și o convenție de denumire a branch-urilor;
- o procedură scrisă pentru publicare, backup și rollback;
- o verificare de compatibilitate între versiunea aplicației și cea a bazei.

## Dependențe și priorități

| Element | Depinde de |
| --- | --- |
| Asocierile notă/paragraf ↔ referințe | `WorkReferences` |
| Căutarea explicațiilor după codul CR-ului | `WorkReferences` și asocierile |
| Evidențierea referințelor în editor | ancore în text actualizate la editare |
| `NotePlatforms` | modulul de platforme și răspunsul la întrebarea despre prima interfață |
| `NoteClients`, `NoteProjects`, `NoteBranches`, `NoteEvents`, `NoteReleases`, `NotePublishes` | modulele respective |
| Lista „Referințe către această notă” | integrarea PR #4 (`NoteReferences`, `NoteReferenceTargets`) |
| Pagina catalogului, titlul și URL-ul unui CR | integrarea PR #4 (`WorkReferences`) și decizia despre păstrarea lor pe context |
| Editarea notelor partajate de colegi | stabilirea permisiunilor |

Ordinea din „Etapele următoare” este cea a planului inițial. TODO: Necesită clarificare — prioritățile efective și versiunea în care intră fiecare etapă.
