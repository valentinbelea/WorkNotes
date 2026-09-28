# Fluxul Git

Regulile obligatorii sunt în [AGENTS.md › Git și limitele sarcinii](../AGENTS.md#git-și-limitele-sarcinii): fără dezvoltare directă pe `main`, fără commit, push, pull request sau merge nesolicitate, fără operații distructive, cu păstrarea modificărilor utilizatorului. Acest document descrie fluxul.

Repository-ul remote este `valentinbelea/WorkNotes` pe GitHub; branch-ul de integrare este `main`.

## Crearea unui feature branch din main

Fiecare sarcină începe dintr-un `main` actualizat, fără operații care aruncă modificări:

```powershell
git status                          # dacă există modificări locale nesalvate: stop, se prezintă situația
git switch main
git pull --ff-only origin main      # actualizare fără merge sau rescriere
git switch -c <nume-branch>
```

- Toate modificările sarcinii se fac numai pe acest branch; branch-ul se publică (`git push -u origin <nume-branch>`) numai la solicitare explicită.
- Dacă `main` local nu se poate actualiza prin fast-forward, nu se forțează nimic: se prezintă situația.

## Convenții de denumire

Nu există încă o convenție aprobată. Denumiri folosite până acum:

| Formă | Exemple | Unde |
| --- | --- | --- |
| `main_task_NN` / `main_task_NNN` | `main_task_00`, `main_task_01`, `main_task_002`, `main_task_02` | taskurile 00–02 (PR #1–#4) |
| `feature/<descriere>` | `feature/project-documentation` | exemplul din cererea de documentare |
| `claude/<descriere>-<id>` | `claude/worknotes-markdown-docs-6xyqw4` | branch-urile create de sesiunile Claude Code pe web |

Numele asemănătoare `main_task_002` (PR #3) și `main_task_02` (PR #4) au produs deja confuzie. TODO: Necesită clarificare — convenția oficială de denumire (prefix, numerotarea taskurilor, număr de cifre).

## Commit-uri

- Numai la solicitare explicită, după verificările din [AGENTS.md › Verificarea livrării](../AGENTS.md#verificarea-livrării).
- Un commit conține un set coerent de modificări; nu amestecă schimbări fără legătură cu sarcina.
- Stilul folosit în istoric: mesaje în engleză, forma `Zonă: descriere` pe primul rând (de exemplu `Board: reorder post-its by drag and drop within a month`, `Contexts: the Owner adds members by email in an overlay`, `Version 0.02: script that records v.0.02 in DatabaseVersion`), urmat, după caz, de un paragraf care explică schimbarea. Commit-urile realizate de Claude Code au trailerul `Co-Authored-By`.
- Nu se fac `amend`, rebase sau force push pe branch-uri publicate fără solicitare explicită.

## Pull request-uri

- Un pull request pentru fiecare task, din branch-ul taskului în `main`, creat numai la solicitare explicită.
- Practica de până acum: titlu descriptiv în engleză (de exemplu „Add drag-and-drop note reordering on the board (version 0.02)”) și o descriere cu rezumatul schimbărilor. Repository-ul nu are șablon de pull request.
- Înainte de a cere integrarea: build și teste reușite, `Test-Resources.ps1` pentru schimbările de localizare, scripturile SQL noi documentate în [Scripts/README.md](../Scripts/README.md), documentația actualizată ([AGENTS.md › Întreținerea documentației](../AGENTS.md#întreținerea-documentației)).

## Code review

Nu există un proces de review documentat și nici revieweri desemnați; până acum pull request-urile au fost create și integrate de proprietarul repository-ului. TODO: Necesită clarificare — cine face review-ul și ce aprobări sunt necesare.

## Merge

- PR-urile #1–#3 au fost integrate prin commit-uri de merge GitHub („Merge pull request #N from valentinbelea/main_task_…”).
- Merge-ul în `main` se face numai la solicitare explicită. Istoricul nu se rescrie.
- Un conflict se rezolvă prin integrarea lui `main` în branch (merge), nu prin rescrierea istoricului unui branch publicat.

## Branch-uri protejate

La 2026-09-25, niciun branch nu are protecție configurată pe GitHub (`main` inclusiv: `protected = false`). TODO: Necesită clarificare — dacă `main` trebuie protejat (de exemplu interzicerea push-ului direct și obligativitatea unui pull request).

## Fișiere generate și secrete

- `.gitignore` exclude `**/bin/`, `**/obj/`, `.vs/`, `*.user`, `*.suo` și `**/node_modules/`.
- Se versionează intenționat fișierele generate: clasele EF produse prin scaffolding (după inspectare, cu LF) și bundle-ul `wwwroot/lib/codemirror/codemirror.js` împreună cu `THIRD-PARTY-NOTICES.txt`, produse numai de `Solution/tools/codemirror` (`npm ci`, apoi `npm run build`); nu se editează manual.
- Nu se versionează secrete: connection string-uri cu utilizator sau parolă, User Secrets, chei Data Protection, fișiere de publicare cu credențiale.
- Fișierele text folosesc LF.

## Pull request-uri deschise

PR #4 (branch `main_task_02`) este deschis la 2026-09-25 și modifică documentele care au fost reorganizate de branch-ul de documentare; impactul și ce trebuie portat sunt în [CURRENT-STATUS.md](CURRENT-STATUS.md#contradicții-identificate).
