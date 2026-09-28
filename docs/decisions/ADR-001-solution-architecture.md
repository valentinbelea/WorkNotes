# ADR-001: Arhitectura soluției

- **Status:** Accepted
- **Data:** documentat retroactiv pe 2026-09-25; decizia se aplică de la începutul proiectului (task 00, version_0.01).
- **Surse:** regulile cerute explicit din [AGENTS.md](../../AGENTS.md); deciziile #1, #6 și #7 din [jurnalul deciziilor](README.md#jurnalul-deciziilor); codul de pe `main`.

## Context

WorkNotes este o aplicație web ASP.NET Core pe .NET 10, cu SQL Server. Aplicația trebuie să separe clar prezentarea, regulile de business și persistența, să permită testarea regulilor fără bază de date, să țină schema bazei sub control explicit, prin scripturi SQL — cerință explicită a utilizatorului —, să fie localizată în trei limbi și să funcționeze fără JavaScript pentru operațiile de bază.

## Decizie

1. **Cinci proiecte, pe straturi:** `WorkNotes.Web` (Razor Pages, prezentare, compunere), `WorkNotes.Business` (servicii, reguli, modele, contracte), `WorkNotes.DataAccess` (EF Core, SQL Server, Identity), `WorkNotes.Resources` (catalogul .resx), `WorkNotes.Business.Tests`. Dependențe: Web → Business, Web → Resources, Web → DataAccess numai pentru compunere, DataAccess → Business; Business nu depinde de nimic.
2. **Contracte în Business:** interfețele serviciilor și ale accesului la date, modelele `record`, regulile `*Rules` și codurile de stare sunt definite în Business; DataAccess implementează repository-urile și contractele de cont; Web compune totul în `Program.cs` prin `AddDataAccess`, cu lifetime scoped.
3. **Database First:** schema este scrisă în scripturi SQL versionate pe foldere de versiune; modelul EF se generează prin scaffolding (`--no-onconfiguring`); nu există migrări EF; Identity este mapat manual în `AccountsDbContext` (`IdentityUserContext<ApplicationUser>`).
4. **Localizare separată:** textele sunt în `WorkNotes.Resources`; Business și DataAccess întorc coduri și chei, iar Web traduce.
5. **Prezentare randată pe server, cu îmbunătățire progresivă:** Razor Pages fără framework SPA; starea overlay-urilor este în URL; aspectul vine numai din clase CSS; JavaScript fără framework adaugă comportamentul.

## Motive

- Database First este o cerință explicită; schema SQL este sursa de adevăr (decizia #1).
- Separarea straturilor și principiile SOLID sunt reguli obligatorii; contractele definite în Business permit testele fără SQL Server (98 de teste la 2026-09-25).
- Starea în URL face ca fiecare dialog să funcționeze fără JavaScript și ca adresele să poată fi deschise direct (decizia #6).
- Stilul numai prin clase CSS răspunde cerinței „fără stiluri din JavaScript” și păstrează ordinea DOM a serviciului (decizia #7).
- Catalogul .resx comun ține Business și DataAccess independente de infrastructura de localizare.

## Alternative cunoscute

- **Migrări EF Core:** abordarea anterioară a folosit istoricul de migrări, eliminat prin scripturile de tranziție menționate în istoricul README-ului (`temp_002_RemoveEFMigrationsHistory.sql`, `003_RemoveIdentityMigrationsHistory.sql`); migrările, snapshot-urile și tabelele de istoric sunt acum interzise.
- **Repository generic, Unit of Work propriu peste EF, mediator, fabrici, proiecte suplimentare:** respinse fără o necesitate demonstrabilă.
- **Acces direct la `DbContext` din pagini:** interzis; Web folosește numai serviciile Business.
- **Tabela `Contexts`:** respinsă, pentru că entitatea generată `Context` ar intra în conflict cu namespace-ul `WorkNotes.DataAccess.Context`; tabela este `WorkContexts`.
- **Randare în client, cu traduceri în JavaScript:** respinsă prin regula de localizare și prin decizia #18 (taburile editorului sunt HTML randat de server).
- **Controllere MVC:** nefolosite; se adaugă numai când există un caz real.

## Consecințe

- Regulile de business sunt testabile fără bază de date, iar granițele se pot verifica automat ([ARCHITECTURE.md](../ARCHITECTURE.md#verificarea-regulilor-arhitecturale)).
- Orice schimbare de schemă cere un script SQL nou, aplicarea lui la cerere explicită, scaffolding-ul (cu toate tabelele în comandă) și, pentru Identity, actualizarea manuală a mapărilor.
- Cheile externe către `Users` există numai în SQL; `ContextMemberRepository` citește membrii și conturile din două contexte, prin interogări separate.
- Web referă DataAccess (numai pentru compunere) și păstrează pachetul `Microsoft.EntityFrameworkCore.Design` pentru tooling.
- Aplicația nu verifică la pornire compatibilitatea dintre cod și schemă ([VERSIONING.md](../VERSIONING.md#legătura-dintre-aplicație-și-baza-de-date)).
- Fiecare funcționalitate trebuie să aibă o variantă fără JavaScript pentru operațiile de bază.
