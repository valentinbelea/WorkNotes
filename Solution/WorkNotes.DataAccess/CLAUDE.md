# WorkNotes.DataAccess — reguli specifice

Proiectul de persistență: EF Core pe SQL Server, repository-uri și implementarea Identity. Regulile generale sunt în [AGENTS.md](../../AGENTS.md); schema și comanda de scaffolding în [docs/DATABASE.md](../../docs/DATABASE.md); scripturile în [Scripts/README.md](../../Scripts/README.md).

## Conținut

- `Context/WorkNotesDbContext.cs` și `Entities/{DatabaseVersion, WorkContext, ContextMember, Note, NoteBlock}.cs` — generate prin scaffolding; `--force` le suprascrie.
- `Context/AccountsDbContext.cs` și `Entities/ApplicationUser.cs` — scrise manual (Identity).
- `Repositories/` — implementările interfețelor din `WorkNotes.Business/Abstractions`.
- `Identity/` — `IdentityAccountService` (contractele de cont, peste `UserManager` / `SignInManager`) și `AccountClaimsPrincipalFactory`.
- `DependencyInjection.cs` — `AddDataAccess`: DbContext-urile, repository-urile și configurarea Identity.

## Reguli

- Database First: nu creați migrări EF, snapshot-uri sau tabele de istoric și nu apelați `Migrate` / `EnsureCreated`. Schema se schimbă printr-un script SQL nou în `Scripts/version_<versiunea curentă>`, aplicat numai la cerere explicită, apoi prin scaffolding cu toate tabelele în comandă.
- Nu editați manual fișierele generate; particularizările merg în fișiere `partial` separate (contextul are `OnModelCreatingPartial`). Aduceți fișierele generate la LF.
- Identity nu se regenerează prin scaffolding: mapările din `AccountsDbContext` se actualizează manual după schema SQL. Politica de parolă și de blocare din `AddDataAccess` trebuie să rămână identică cu cea din [AGENTS.md](../../AGENTS.md#identitate-și-autentificare) și cu `AccountRules`.
- Fiecare citire și modificare aplică filtrul de acces: `ForMember(userId)` pentru contexte, `VisibleTo(userId)` pentru note, plus proprietarul pentru modificări. Nicio metodă nouă nu ocolește aceste filtre.
- Citirile folosesc `AsNoTracking` și proiecții în modele Business; entitățile nu ies din proiect. Pentru nume identice folosiți alias-uri (`using NoteEntity = WorkNotes.DataAccess.Entities.Note;`).
- Toate apelurile EF sunt asincrone și primesc `CancellationToken`; nu există operații concurente pe același DbContext.
- Erorile SQL așteptate se traduc în coduri de stare: 2601/2627 (unicitate), 547 (cheie externă), `DbUpdateConcurrencyException` (conflict); după eșec, entitatea se detașează sau `ChangeTracker` se golește. Celelalte excepții se propagă.
- Concurența notelor se face prin `RowVersion` (token Base64 de 8 octeți): salvarea setează valoarea originală și actualizează mereu rândul notei; schimbul ordinii este un singur `UPDATE` condiționat de versiunile ambelor note. `[Order]` se scrie mereu între paranteze drepte în SQL-ul scris manual; ordinea minimă se citește cu `UPDLOCK, HOLDLOCK`, în tranzacție.
- Valorile `datetime2` citite primesc `DateTimeKind.Utc`; `ModifiedAtUtc` egal cu crearea înseamnă notă nemodificată.
- Lifetime-urile rămân scoped; connection string-ul vine numai din parametrul lui `AddDataAccess`, niciodată din cod.
