# WorkNotes.Business — reguli specifice

Proiectul regulilor de business. Regulile generale sunt în [AGENTS.md](../../AGENTS.md); entitățile și regulile de domeniu în [docs/DOMAIN-MODEL.md](../../docs/DOMAIN-MODEL.md).

## Conținut

- `Abstractions/` — interfețele serviciilor (`INoteService`, `IWorkContextService`, `IContextMemberService`, `IApplicationVersionService`, `IAccountService`, `IAuthenticationService` și, cu PR #4, `INoteReferenceService`, `IReferenceTypeService`; în 0.03, `IGitHubConnectionService`, `IGitHubTokenService`, `IGitRepositoryService`) și ale accesului la date (`INoteRepository`, `IWorkContextRepository`, `IContextMemberRepository`, `IApplicationVersionRepository`; PR #4: `INoteReferenceRepository`, `IReferenceTypeRepository`; 0.03: `IGitConnectionRepository`, `IGitRepositoryRepository`), plus clientul furnizorului GitHub, `IGitHubOAuthClient`, implementat în `WorkNotes.Integrations`.
- `Services/` — `NoteService`, `WorkContextService`, `ContextMemberService`, `ApplicationVersionService`; PR #4: `NoteReferenceService`, `ReferenceTypeService` și `ReferenceTypeCache` (singleton: numai parserul tipurilor de referință și momentul citirii, fără dependențe); 0.03: `GitHubConnectionService` (conectarea OAuth, verificarea și deconectarea contului GitHub), `GitHubTokenService` (tokenul valid, cu reîmprospătare) și `GitRepositoryService` (importul repository-urilor).
- `Models/` — DTO-uri `sealed record`, reguli (`NoteRules`, `WorkContextRules`, `AccountRules`; PR #4: `NoteReferenceRules` și `NoteReferenceParser`, singurul loc care citește referințele din text, cu tipurile din `dbo.ReferenceTypes` date de `IReferenceTypeService`), constante (`NoteTypes`, `NoteVisibilities`, `ContextRoles`, `GitProviders`); 0.03: `GitAuthorizationRules` (state, PKCE, reîmprospătarea tokenurilor), `GitRepositoryRules` (limitele și normalizarea repository-urilor) și modelele conexiunii Git (`GitConnection` fără tokenuri, `GitCredential` și `GitTokens` cu ele, care își lasă tokenurile afară din `ToString()`) și coduri de stare (`*Status`).

## Reguli

- Proiectul nu are referințe de pachete sau de proiecte și nu folosește EF Core, ASP.NET Core, Identity, SQL sau localizarea. Nu adăugați astfel de dependențe.
- Serviciile încep cu `ArgumentException.ThrowIfNullOrWhiteSpace(userId)` și `cancellationToken.ThrowIfCancellationRequested()`, validează și normalizează intrarea prin `*Rules`, verifică apartenența la context și proprietatea prin repository-uri, apoi apelează persistența. Tokenul primit se transmite mai departe, neschimbat.
- Rezultatele sunt coduri de stare sau chei de mesaj, niciodată texte traduse, coduri HTTP sau excepții pentru cazuri așteptate.
- Contractele de repository întorc modele Business; nu expun entități, `IQueryable` sau tipuri SQL. O metodă nouă de repository are un comentariu care spune ce filtru de acces aplică și ce înseamnă un rezultat null sau false.
- Limitele din `*Rules` corespund coloanelor SQL, iar constantele din `NoteTypes`, `NoteVisibilities` și `ContextRoles` corespund constrângerilor `CHECK`; o schimbare aici cere un script SQL nou ([Scripts/README.md](../../Scripts/README.md)).
- Timpul vine din `TimeProvider`: ziua jurnalului și luna de pe tablă folosesc calendarul local al aplicației, iar auditul folosește UTC, trunchiat la secundă.
- Regulile de acces nu se relaxează: contextul este granița de acces; numai proprietarul modifică o notă; numai `Owner` modifică un context și îi gestionează membrii.
- Orice regulă nouă sau modificată primește teste în `WorkNotes.Business.Tests`, cu stub-uri scrise manual și fără bază de date ([docs/TESTING.md](../../docs/TESTING.md)).
