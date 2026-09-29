# Cerințe funcționale

Statusurile sunt stabilite după codul de pe `main` (commit `504c01e`, 2026-09-25), nu numai după cerințele scrise. Comportamentul detaliat al interfeței este în [UI-UX.md](UI-UX.md), regulile de business în [DOMAIN-MODEL.md](DOMAIN-MODEL.md).

| Marcaj | Sens |
| --- | --- |
| [x] | Implementat |
| [ ] | Neimplementat |
| [~] | În dezvoltare (de exemplu într-un pull request neintegrat) |
| [!] | Necesită remediere sau clarificare |

## Dashboard

- [x] Pagina principală este tabla contextului selectat, pentru utilizatorul autentificat; vizitatorii văd panoul de bun venit, cu Autentificare și Înregistrare.
- [x] O tablă pentru fiecare context; lista de contexte înlocuiește titlul tablei (`/?context={id}`, implicit primul context); fără JavaScript, butonul „Afișează”.
- [x] Subtitlul „Spațiul meu” în header, dedus din secțiunea meniului.
- [x] Starea goală („Tabla este goală.”) și butonul „Notă nouă” dezactivat când utilizatorul nu are contexte, cu link către pagina Contexte.
- [!] Textul panoului de bun venit, „Aplicația este pregătită pentru dezvoltare.”, pare provizoriu. TODO: Necesită clarificare.

## Post-it-uri

- [x] Fiecare notă este un post-it: galben #FFF0B7 pentru jurnal, salvie #DDEADB pentru articol, cu bandă adezivă în culoarea hârtiei și înclinări deterministe alese de server din ID.
- [x] Headerul cardului: data creării (stânga) și a ultimei modificări (dreapta), pe același rând, cu anul, fără etichete vizibile; data modificării lipsește cât timp nota nu a fost modificată.
- [x] Previzualizarea primelor paragrafe; footerul cu vizibilitatea, Open și, pentru proprietar, Delete.
- [x] Ștergerea unei note, numai de proprietar, cu confirmare peste tablă; paragrafele se șterg odată cu nota.

## Grupuri

- [x] Notele sunt grupate pe luni după data locală a ultimei modificări, cele mai noi luni primele; luna curentă există mereu ca țintă pentru nota nouă.
- [x] În fiecare lună: `Order` crescător, apoi ultima modificare, crearea și `Id`, cele mai recente primele.
- [!] În cod și în documentele existente „grupul” este luna. TODO: Necesită clarificare — dacă termenul trebuie să acopere și alte grupări (de exemplu grupuri definite de utilizator).

## Jurnal / articol

- [x] Două tipuri de notă: jurnal (datat cu ziua locală) și articol (fără dată).
- [~] Schimbarea tipului unei note în editor, din jurnal în articol și invers, salvată odată cu nota (o notă devenită jurnal este datată cu ziua creării) — implementată în PR #4, neintegrată în `main`.
- [x] Mai multe jurnale pe zi.
- [x] Titlul este opțional, cel mult 200 de caractere; o notă fără titlu apare „Fără titlu”.
- [x] Notele noi sunt private; o notă `Context` este vizibilă membrilor contextului, numai pentru citire.

## Creare și editare inline

- [x] „Notă nouă” direct pe tablă: cardul apare primul în luna curentă, cu switch jurnal/articol, titlu opțional, Salvează / Renunță, Escape; fără JavaScript prin `/?new=true`.
- [x] Redenumirea titlului pe loc (Enter sau părăsirea câmpului salvează, Escape anulează), numai de proprietar; fără JavaScript, prin formular.
- [x] Mesajele de salvare (succes, avertisment, eroare) fixe sus pe centru, până le închide utilizatorul, și în dialoguri.

## Editor

- [x] Editor CodeMirror 6 peste tablă, într-un dialog de 90% din fereastră, deschis cu Open sau dublu-click (`/?note={id}`).
- [~] Deschiderea unei note de pe tablă fără reîncărcarea paginii — implementată în PR #4, neintegrată în `main`.
- [~] Tabla din spatele editorului urmează fiecare salvare (titlul și previzualizarea când se schimbă, data ultimei modificări la fiecare salvare, trecerea unei note mai vechi în luna curentă), iar închiderea editorului fără modificări nesalvate nu reîncarcă pagina — implementate în PR #4, neintegrate în `main`.
- [x] Paragrafe cu identitate stabilă și audit propriu; bara de informații arată data creării și a modificării paragrafului de sub mouse sau de la cursor.
- [x] Căutare/înlocuire, undo/redo, rândul activ, Ctrl+S, avertizare la părăsirea paginii cu modificări nesalvate.
- [~] Selecția textului vizibilă în editor, pe foaia jurnalului și pe cea a articolului, inclusiv pe rândul activ și în titlu — corecție în PR #4, neintegrată în `main`.
- [x] Detectarea salvărilor concurente (`RowVersion`): o salvare dintr-un editor învechit este refuzată, fără să suprascrie.
- [x] Note partajate numai pentru citire pentru ceilalți membri; fără JavaScript, conținutul se afișează numai pentru citire.
- [x] Contextul se alege pe tablă, înainte de deschiderea editorului; editorul îl afișează, nu îl schimbă.
- [ ] Salvarea automată.
- [ ] Marcajul de paragraf important (`IsImportant`) și data activității (`ActivityDate`).

## Editor cu mai multe taburi

- [x] Mai multe note deschise simultan, în taburi independente (conținut, titlu, modificări nesalvate, undo, versiune, stare).
- [x] O notă deschisă de pe tablă cât timp editorul este în pagină se adaugă ca tab nou, fără reîncărcare; o notă deja deschisă își activează tabul.
- [x] × pe un tab închide doar nota lui (cu confirmare dacă are modificări nesalvate); Închide din header închide toate taburile.
- [!] La reîncărcarea paginii se redeschide doar tabul activ (limitare cunoscută).

## Minimizare și maximizare

- [x] Minimizarea ascunde editorul fără să salveze sau să piardă ceva și afișează în stânga-jos forma compactă (tip, titlu, data creării și a ultimei modificări, Maximizează, Închide); tabla se poate folosi între timp.
- [x] Maximizează sau dublu-click pe forma compactă readuce editorul, cu toate taburile.

## Ordonare drag-and-drop în cadrul grupului

- [x] Proprietarul schimbă locul a două note ale sale din aceeași lună, prinzând cardul de bandă; schimbul se salvează imediat (`?handler=SwapNotes`), iar la eșec luna revine la ordinea anterioară, cu mesaj localizat.
- [x] Drop-ul în altă lună, pe nota altui membru sau în afara cardurilor este ignorat; editorul deschis în aceeași pagină salvează în continuare după un schimb.
- [~] Un drag nou nu mai este blocat cât timp un schimb anterior se salvează (corecție în PR #4).
- [!] Reordonarea funcționează numai cu mouse-ul; nu există alternativă de la tastatură (limitare cunoscută).

## Referințe interne între note

Implementate în PR #4 (branch `main_task_02`), neintegrate în `main`; regulile sunt în [DOMAIN-MODEL.md](DOMAIN-MODEL.md#în-dezvoltare) și [ADR-003](decisions/ADR-003-internal-references.md):

- [~] Recunoașterea referințelor scrise în paragrafe, cu tipurile configurate (implicit CR și bug): `CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `bug 1234`, `bug-1234`, `bug_1234`, `bug1234`, cu tipul în orice combinație de litere mari și mici, ca termeni întregi (`XCR30080A` nu este referință), comparate după tip și număr (`CR:30080`).
- [~] Tipurile de referință într-o tabelă de configurare (`ReferenceTypes`: prefixul, 1–10 litere ASCII, activ sau nu), folosite de toată aplicația dintr-o colecție ținută în memorie (aceeași pentru toți utilizatorii, citită din nou după cel mult 5 minute); un tip nou (de exemplu `TASK`) se adaugă fără cod nou.
- [~] Destinațiile: toate notele contextului, vizibile proprietarului paragrafului, cu aceeași referință în titlu, în afară de nota paragrafului (`bug1234` → „Rezolvare Bug-1234”; `CR 27881` → „CR_27881” și celelalte note cu CR-ul în titlu); fără nicio notă, nicio legătură (cazurile sunt listate de `012_RefreshNoteReferences.sql`, înainte de `008` de `005_CreateNoteReferenceTargets.sql`).
- [~] Legăturile stocate pe paragraf (`NoteReferences`), cu notele fiecărei referințe într-o tabelă separată (`NoteReferenceTargets`, legătură 1–M), recalculate automat la salvarea paragrafelor (creare, modificare, ștergere), la schimbarea titlului unei note și la crearea sau ștergerea unei note; textul paragrafelor nu se modifică.
- [~] Catalogul referințelor (`WorkReferences`): fiecare referință stocată, tipul CR sau bug și numărul, o singură dată (cheia unică tip + număr), cu ID propriu, care însoțește în `NoteReferences` textul fiecărei referințe (`WorkReferenceId`); o referință nouă intră în catalog la prima ei salvare.
- [~] Afișarea în editor: fiecare apariție a unei referințe cu destinație, oricare îi este forma, este link către notele ei; click sau Ctrl+Enter le deschide pe toate, fiecare într-un tab nou sau în tabul ei existent, și o arată pe prima; readuce editorul minimizat și păstrează celelalte taburi cu modificările lor; fără JavaScript, linkuri HTML (câte unul pentru fiecare notă).
- [~] Sertarul referințelor din editor, în dreapta textului: fiecare referință a notei care este link, o singură dată, în ordinea din text, ca un singur link scris cum îl scrie textul prima dată (`CR_30080`), cu notele lui în tooltip; click deschide toate notele lui în taburi; lista urmează fiecare salvare; antetul este opac; fără JavaScript, linkuri către `/?note={id}`; pe ecranele înguste, sub text.
- [~] Popup-ul referinței abia scrise: un spațiu, Tab, un rând nou sau un semn de punctuație scris imediat după un număr caută, pe server, referința cu care se termină textul; pentru o referință cu note, popup-ul le arată cu butonul „Transformă în referință” (și Tab), care o face link imediat; pentru o referință fără notă, „Referință inexistentă”, închis singur după câteva secunde; pentru un text care nu este referință, nimic.
- [~] Reindexarea conținutului existent, inclusiv a jurnalului „CRs”, cu `Scripts/version_0.02/004_ReplaceNoteReferences.sql`, apoi, pentru mai multe note pe referință, cu `005_CreateNoteReferenceTargets.sql`, iar catalogul referințelor existente cu `006_CreateWorkReferences.sql`, `007_InsertWorkReferences.sql` și `008_UpdateNoteReferencesWorkReferenceId.sql`, tipurile cu `009_CreateReferenceTypes.sql`–`011_UpdateReferenceTypeKeys.sql`, iar după o schimbare a tipurilor, textele deja scrise cu `012_RefreshNoteReferences.sql` (aplicate numai la cerere explicită).
- [!] Previzualizarea cardurilor afișează textul fără linkuri; o referință nou scrisă devine link la salvare sau, mai devreme, din popup; sertarul o arată abia după salvare.
- [ ] Lista „Referințe către această notă” (backlog-ul PR #4).
- [ ] Pagina catalogului de referințe de lucru CR/bug (`WorkReferences`, creat în PR #4, comun tuturor contextelor), cu titlul și URL-ul extern ale unei referințe, și asocierile cu notele și paragrafele (`NoteWorkReferences`, `NoteBlockWorkReferences`), cu căutarea explicațiilor după codul CR-ului.
- [ ] Legături externe pe notă sau pe paragraf (`NoteLinks`).
- [ ] Evidențierea referințelor de lucru din catalog în text (ancore actualizate la editare), autocomplete și popup-uri pentru referințe.

## Contexte și membri

- [x] Listarea contextelor în care utilizatorul este membru, ordonate după nume; adăugare, editare și ștergere în overlay pe aceeași pagină; linkul „Contexte” din meniu.
- [x] Creatorul devine proprietar (`Owner`); numai proprietarul editează, șterge și gestionează membrii; un membru care încearcă direct adresa primește 403, iar un context străin răspunde 404.
- [x] Adăugarea unui membru după e-mail (rolul `Member`) și eliminarea membrilor, cu mesaje distincte pentru e-mail invalid, cont inexistent și membru existent.
- [x] Un context care are note nu poate fi șters (avertisment); numele contextului este unic global.
- [!] Contextele create înainte de scriptul `005_CreateContextMembers.sql` nu au membri, deci nu sunt vizibile nimănui în aplicație, iar numele lor rămân ocupate. TODO: Necesită clarificare — dacă există astfel de contexte și cum se tratează.

## Autentificare și cont utilizator

- [x] Înregistrare (prenume, nume, e-mail unic, parolă și confirmare); după creare se deschide autentificarea.
- [x] Autentificare cu „Ține-mă minte”; mesaj unic de eșec; blocare 15 minute după 5 încercări eșuate.
- [x] „Contul meu”: modificarea numelui și prenumelui; e-mailul este afișat și nu se poate modifica.
- [x] Schimbarea parolei, cu verificarea parolei actuale și reîmprospătarea sesiunii.
- [x] Deconectare numai prin POST.
- [ ] Roluri, confirmarea e-mailului, recuperarea parolei — excluse explicit până la o cerință nouă.

## Sistemul multilingv

- [x] Română (implicită și fallback), engleză și poloneză pentru toate textele interfeței, prin .resx.
- [x] Selectorul de limbă din header, aplicat imediat, cu cookie păstrat un an; funcționează și fără JavaScript.
- [x] Mesajele de validare, erorile Identity și textele editorului sunt localizate.
- [!] Datele sunt afișate în același format în toate limbile. TODO: Necesită clarificare ([LOCALIZATION.md](LOCALIZATION.md#formatarea-datelor)).

## Integrarea cu Git (version_0.03)

Versiunea 0.03 este dedicată integrării cu Git, la cererea utilizatorului din 2026-09-29: furnizorul este GitHub (deocamdată), iar WorkNotes va citi branch-urile, commit-urile și pull request-urile legate de CR-uri și buguri. Primul pas, în lucru pe branch-ul `main_task_03`, este conectarea contului GitHub ([ADR-004](decisions/ADR-004-github-oauth.md)).

- [~] Versiunea 0.03: `Scripts/version_0.03/000_UpdateDatabaseVersion.sql` și documentele de versionare.
- [~] Secțiunea Conectare GitHub din Contul meu (`/Account/GitHub`): conectarea prin OAuth (cu PKCE și `state`), contul afișat, verificarea conexiunii (cu reîmprospătarea tokenului), reconectarea și deconectarea (cu revocarea autorizării la GitHub); tokenurile criptate în `dbo.GitConnections` (`001_CreateGitConnections.sql`); proiectul nou `WorkNotes.Integrations`.
- [~] Importul repository-urilor (`/Repositories`, în meniu): lista tuturor repository-urilor GitHub ale contului conectat, cu câte o bifă, iar salvarea face din cele bifate repository-urile importate în WorkNotes (`dbo.GitRepositories`, `002_CreateGitRepositories.sql`); cel mult 1000 afișate.
- [~] Referința Git din editor (popup-ul referinței abia scrise): opțiunea existentă se numește „Referință aplicație”, iar „Referință Git” alege un repository importat și caută branch-urile al căror nume conține referința (aceeași regulă ca în texte: tipul din `ReferenceTypes`, separatorul, numărul, cuvânt întreg); alegerea unui branch îl leagă de paragraf (`dbo.GitReferences`, `dbo.NoteBlockGitReferences`, `003_CreateGitReferences.sql`). Legăturile apar în sertarul referințelor, cu linkul branch-ului, și se pot elimina; se arată cât timp paragraful scrie referința. Numai proprietarul notei; cel mult 1000 de branch-uri citite, 50 afișate.
- [ ] Citirea commit-urilor și pull request-urilor legate de CR-uri și buguri, din repository-urile importate (branch-urile sunt implementate mai sus). TODO: Necesită clarificare — ce repository-uri se citesc (configurate pe context?) și după ce se leagă de un CR sau bug (numele branch-ului, mesajul commit-ului, titlul pull request-ului).
- [ ] Alți furnizori Git (GitLab, Azure DevOps), numai la o cerere nouă.

## Alte cerințe

- [x] Versiunea aplicației, citită din `DatabaseVersion`, în footer.
- [x] Designul „Hârtie & salvie” și sigla WN în fereastra editorului și ca favicon.
- [x] Funcționarea de bază fără JavaScript.
- [ ] Schimbarea vizibilității unei note (`Private` / `Context`) din interfață.
- [ ] Arhivarea (`ArchivedAtUtc`) și afișarea notelor arhivate.
- [ ] Editarea unei note partajate de către colegi (după stabilirea permisiunilor).
- [ ] Lista de contexte cu stil propriu pentru opțiunile deschise (acum este `select` nativ).
- [ ] Un tip de mesaj „info” separat, dacă apare un mesaj care are nevoie de el.
- [ ] Platformele (`NotePlatforms`) și, ulterior, clienții, proiectele, branch-urile, evenimentele, release-urile și publish-urile.
- [ ] Înlocuirea jurnalelor TXT: TODO: Necesită clarificare — dacă este nevoie de importul conținutului existent ([PROJECT-CONTEXT.md](PROJECT-CONTEXT.md#originea-proiectului)).

## Sinteză

**Implementate** (version_0.01 și version_0.02): conturile, localizarea ro/en/pl, designul, contextele și membrii, tabla pe contexte și luni, post-it-urile cu creare, redenumire și ștergere pe loc, editorul cu paragrafe auditate, taburi și minimizare, mesajele de salvare, ordonarea prin drag-and-drop, versiunea în footer.

**În lucru**: PR #4 — schimburi succesive prin drag-and-drop fără blocare, deschiderea și închiderea notelor fără reîncărcarea tablei, care urmează salvările din editor, schimbarea tipului unei note din editor și referințele interne CR/bug între note, cu reindexarea conținutului existent, sertarul referințelor din editor și popup-ul referinței abia scrise, plus corecția selecției din editor.

**În lucru pentru version_0.03**: conectarea contului GitHub prin OAuth și importul repository-urilor.

**Planificate**: referințele de lucru CR/bug și asocierile lor, legăturile externe, evidențierea referințelor, salvarea automată, vizibilitatea și arhivarea din interfață, paragraful important, editarea notelor partajate, platformele și modulele ulterioare. Ordinea și dependențele sunt în [ROADMAP.md](ROADMAP.md).
