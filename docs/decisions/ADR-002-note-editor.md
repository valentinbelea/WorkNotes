# ADR-002: Editorul de note

- **Status:** Accepted — pentru editorul existent pe `main`. Extensiile propuse în PR #4 (referințele interne) nu fac parte din această decizie.
- **Data:** documentat retroactiv pe 2026-09-25; editorul a fost implementat în version_0.01 (task 01, integrat prin PR #2).
- **Surse:** deciziile #2, #8, #9, #10, #16–#20 și #31 din [jurnalul deciziilor](README.md#jurnalul-deciziilor); codul din `note-editor.js`, `NoteService` și `NoteRepository`.

## Context

Notele sunt jurnale cronologice și articole tematice. Utilizatorul are nevoie de un editor de text rapid, cu căutare și undo, în care să poată ține mai multe note deschise; fiecare paragraf trebuie să-și păstreze auditul (când a fost creat și când a fost modificat), independent de lățimea ferestrei. Editorul nu trebuie să aibă cost de licență sau servicii externe, textele interfeței trebuie să vină din .resx, iar salvările din ferestre diferite nu trebuie să se suprascrie.

## Decizie

1. **Biblioteca:** CodeMirror 6 (licență MIT), inclus local ca bundle ES construit cu esbuild din `Solution/tools/codemirror` (versiuni fixate în `package.json` / `package-lock.json`, numai modulele `state`, `view`, `commands`, `search`), cu `THIRD-PARTY-NOTICES.txt` alături (decizia #8).
2. **Modelul conținutului:** nota are două niveluri — `Notes` (organizare) și `NoteBlocks` (text și audit pe paragraf) (decizia #2). Un paragraf este textul dintre rânduri goale; ID-ul lui este un GUID generat de editor, păstrat la editare, pe primul fragment la împărțire și pe primul paragraf la unire (decizia #9).
3. **Salvarea:** editorul trimite toată nota (paragrafele în ordine) prin POST JSON cu antiforgery; serverul compară paragrafele cu cele salvate și scrie numai diferențele; `RowVersion` al notei verifică fiecare salvare, iar UPDATE-ul notei rulează mereu (decizia #10).
4. **Fereastra:** dialog peste tablă (90% din fereastră); taburile noi se încarcă ca HTML randat de server (decizia #18); minimizarea transformă dialogul în nemodal, fără să distrugă editorul (decizia #17); închiderea unui tab cu modificări nesalvate se confirmă cu `window.confirm` (decizia #19); starea din bara editorului rămâne lângă mesajele de salvare (decizia #16); contextul se alege pe tablă, iar editorul doar îl afișează (decizia #20); după un schimb pe tablă, taburile primesc noile versiuni (decizia #31).

## Motive

- CodeMirror 6: fără cost de licență, inclus local, fără serviciu cloud.
- Paragraful definit prin rânduri goale face auditul independent de lățimea ferestrei și aplică regulile de împărțire și unire din plan.
- Verificarea `RowVersion` la fiecare salvare evită suprascrierea din alt tab, inclusiv la două salvări în aceeași secundă.
- Taburile randate de server păstrează textele din .resx și markup-ul pe server, fără reîncărcarea paginii.
- Minimizarea fără distrugerea editorului nu pierde nimic și lasă tabla utilizabilă.
- Avertizarea browserului funcționează doar la părăsirea paginii, deci închiderea unui singur tab are nevoie de o confirmare proprie.

## Alternative cunoscute

- **Paragraful ca rând vizual:** respins — auditul ar depinde de lățimea ferestrei.
- **Taburi randate în JavaScript, cu textele în script:** respinse — traducerile nu se duplică în JavaScript.
- **`beforeunload` pentru închiderea unui tab:** nu se aplică, pentru că pagina nu este părăsită.
- **Distrugerea editorului la minimizare:** respinsă — s-ar pierde modificările nesalvate și istoricul undo.
- **Salvarea automată:** nedecisă; figurează ca îmbunătățire ([ROADMAP.md](../ROADMAP.md#îmbunătățiri)).
- **Alte biblioteci de editare:** TODO: Necesită clarificare — alternativele evaluate nu sunt documentate.

## Consecințe

- Auditul paragrafelor rămâne stabil la editare, mutare și redimensionare; salvările concurente primesc conflict, fără suprascriere.
- Bundle-ul CodeMirror se actualizează numai prin `npm ci` și `npm run build` în `Solution/tools/codemirror` și se versionează împreună cu notificările de licență.
- Limitări acceptate: mutarea unui paragraf prin tăiere și lipire creează un paragraf nou; la reîncărcare se redeschide doar tabul activ; fără JavaScript nota este numai pentru citire; un editor deschis în altă fereastră trebuie reîncărcat după un conflict.
- Limitele unei salvări (5000 de paragrafe, 1 000 000 de caractere) sunt verificate pe server.
- Formatul referințelor interne propus în PR #4 (păstrat în textul paragrafului) va primi un ADR propriu la integrare.
