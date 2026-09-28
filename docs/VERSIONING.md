# Versionare

Regulile obligatorii sunt în [AGENTS.md](../AGENTS.md#scripturi-sql-și-versiuni): scripturile noi merg numai în folderul versiunii curente, versiunea se schimbă numai la solicitare explicită, iar scripturile livrate nu se modifică. Lista scripturilor este în [Scripts/README.md](../Scripts/README.md).

## Schema de versionare

- Versiunea WorkNotes este versiunea bazei de date: o etichetă text înregistrată în `dbo.DatabaseVersion.Version`, în formatul `v.major.minor[.build[.revision]]`, de exemplu `v.0.01` și `v.0.02`.
- Versiunea 0.0x corespunde etichetei `v.0.0x` și folderului `Scripts/version_0.0x`.
- Nu există o versiune separată a assembly-urilor (proiectele nu declară `Version`), tag-uri Git sau release-uri; nu există o legătură automată între versiune și branch-uri (verificat la 2026-09-25).
- Versiunile corespund taskurilor: version_0.01 cuprinde taskurile 00 și 01, iar version_0.02 începe cu taskul 02.

## Versiunea curentă

**0.02** — eticheta `v.0.02`, folderul `Scripts/version_0.02`.

| Versiune | Conținut principal | Integrare în `main` |
| --- | --- | --- |
| 0.01 | Conturi, localizare, design „Hârtie & salvie”, contexte și membri, note pe tablă, editorul cu paragrafe, taburi și minimizare | PR #1 (2026-09-22), PR #2 (2026-09-25) |
| 0.02 | Înregistrarea `v.0.02`; ordonarea post-it-urilor prin drag-and-drop (`Notes.[Order]`) | PR #3 (2026-09-25); PR #4 este deschis |

Detaliile sunt în [CHANGELOG.md](../CHANGELOG.md). Nu se creează o versiune nouă fără solicitare explicită.

## Tabela DatabaseVersion

- Structura: o singură coloană, `Version nvarchar(50) NOT NULL`, cheie primară `PK_DatabaseVersion` (scriptul `version_0.01/000_CreateDatabaseVersion.sql`).
- Fiecare versiune adaugă un rând, numai dacă lipsește; rândurile versiunilor anterioare rămân (`v.0.01` rămâne lângă `v.0.02`).
- Tabela nu are dată de instalare, deci versiunea curentă este cea mai mare versiune numerică înregistrată. Compararea este numerică (`v.0.10` urmează după `v.0.9`), eticheta originală se afișează nemodificată, iar etichetele care nu se pot interpreta numeric au prioritatea cea mai mică și sunt ordonate determinist după text. Regula este în `ApplicationVersionService` și este acoperită de `ApplicationVersionServiceTests`.
- Web afișează versiunea în dreapta-jos a footerului; o tabelă goală afișează „Versiune neconfigurată”, iar o eroare de conexiune nu este tratată ca tabelă goală. Versiunea nu se hardcodează în Web sau Business.

## Folderele și ordinea scripturilor

- Câte un folder pentru fiecare versiune: `Scripts/version_0.01`, `Scripts/version_0.02`. În fiecare folder, scripturile sunt numerotate `NNN_Descriere.sql` de la `000` și se aplică în ordinea numelor.
- Folderele se aplică în ordinea versiunilor: întâi toate scripturile din `version_0.01`, apoi cele din `version_0.02`.
- Primul script al unei versiuni înregistrează versiunea: `000_CreateDatabaseVersion.sql` și `001_InsertDatabaseVersion.sql` în 0.01, `000_UpdateDatabaseVersion.sql` în 0.02.
- Un modul nou nu schimbă versiunea: scripturile lui se adaugă, cu numărul următor, în folderul versiunii curente.

## Legătura dintre aplicație și baza de date

- Aplicația afișează versiunea bazei, dar nu verifică la pornire dacă baza are toate scripturile cerute de cod și nu aplică scripturi. Codul unei versiuni presupune că toate scripturile versiunii respective au fost aplicate; de exemplu, codul version_0.02 citește `Notes.[Order]`.
- TODO: Necesită clarificare — dacă se dorește o verificare de compatibilitate între versiunea aplicației și cea a bazei.

## Upgrade

1. Backup al bazei — TODO: Necesită clarificare (procedura nu este definită; vezi [DEPLOYMENT.md](DEPLOYMENT.md#backup)).
2. Aplicarea, la cerere explicită, a scripturilor lipsă, în ordine, conform [Scripts/README.md](../Scripts/README.md). Scripturile sunt idempotente, deci rularea unui script deja aplicat nu schimbă nimic.
3. Verificarea după aplicare (versiunea din `DatabaseVersion`, obiectele create) — [Scripts/README.md](../Scripts/README.md#verificarea-după-aplicare).
4. Publicarea aplicației și verificarea footerului — [DEPLOYMENT.md](DEPLOYMENT.md).

## Rollback

- Nu există scripturi de rollback: scripturile sunt numai înainte, iar rândurile din `DatabaseVersion` nu se șterg.
- TODO: Necesită clarificare — procedura de rollback a bazei și a aplicației (de exemplu restaurarea unui backup) nu este definită.

## Scripturile livrate

- Un script livrat (integrat în `main`) nu se modifică retroactiv: o corecție se face printr-un script nou, în folderul versiunii curente. Motivul practic: verificările de idempotență (`IF OBJECT_ID … IS NULL`, `IF COL_LENGTH … IS NULL`) sar peste obiectele existente, deci o modificare ulterioară a unui script deja aplicat nu ar ajunge în bazele respective.
- TODO: Necesită clarificare — dacă un script aplicat pe un mediu comun (Test, Production) înainte de integrarea în `main` este considerat și el livrat.

## Schimbarea versiunii

Numai la solicitare explicită, după precedentul version_0.02 (decizia #22 din [jurnalul deciziilor](decisions/README.md#jurnalul-deciziilor)):

1. Se creează folderul `Scripts/version_<versiune>`.
2. Primul script, `000_UpdateDatabaseVersion.sql`, inserează eticheta nouă în `DatabaseVersion` numai dacă lipsește, în tranzacție, cu `UPDLOCK, HOLDLOCK`, fără a modifica versiunile anterioare.
3. Se actualizează [secțiunea Versiunea curentă](#versiunea-curentă), regula din [AGENTS.md](../AGENTS.md#scripturi-sql-și-versiuni), [README.md](../README.md), [Scripts/README.md](../Scripts/README.md) și [CHANGELOG.md](../CHANGELOG.md).
