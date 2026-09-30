# WorkNotes.Integrations — reguli specifice

Clienții serviciilor externe (versiunea 0.03: GitHub OAuth și API), peste `HttpClient`. Regulile generale sunt în [AGENTS.md](../../AGENTS.md), în special [Integrarea GitHub](../../AGENTS.md#integrarea-github); decizia este [ADR-004](../../docs/decisions/ADR-004-github-oauth.md); protecțiile în [docs/SECURITY.md](../../docs/SECURITY.md#conectarea-github).

## Conținut

- `GitHub/GitHubOAuthClient.cs` — implementează `IGitHubOAuthClient` (Business): adresa de autorizare, schimbul codului și reîmprospătarea tokenului (`/login/oauth/access_token`), contul (`GET /user`), repository-urile (`GET /user/repos`, pagină cu pagină după antetul `Link`, cel mult `GitRepositoryRules.MaxListed`) și revocarea (`DELETE /applications/{client_id}/grant`).
- `GitHubOAuthClient` citește și branch-urile unui repository (`GetBranchesAsync`, paginat după `Link: rel="next"`, cel mult 1000) și verifică un branch (`GetBranchAsync`, `NotFound` când nu există).
- `GitHub/GitHubOptions.cs` — secțiunea de configurare `GitHub` (numai adresele și timeout-ul nesensibile).
- `DependencyInjection.cs` — `AddIntegrations`: opțiunile și clientul tipizat, apelat numai din `Program.cs`.

## Reguli

- Proiectul referă numai `WorkNotes.Business` și `FrameworkReference Microsoft.AspNetCore.App`; nu referă Web, DataAccess sau EF Core și nu accesează baza de date.
- Contractele sunt definite în `WorkNotes.Business/Abstractions`; clienții întorc modele Business și coduri de stare (`GitProviderResult<T>`, `GitProviderStatus`): `Rejected` când furnizorul a refuzat, `Unavailable` când nu a răspuns (eroare de rețea, timeout, 5xx, 403/429, un corp care nu este JSON). Nu aruncă excepții pentru aceste cazuri și nu întorc texte traduse.
- Anularea cererii se propagă (`OperationCanceledException`); numai timeout-ul `HttpClient` devine `Unavailable`.
- Tokenurile și `ClientSecret` se trimit numai în corpul cererilor și în antetul `Authorization`, niciodată în adrese, și nu se jurnalizează. Clientul nu păstrează tokenuri: le primește și le întoarce.
- Endpoint-urile și timeout-ul vin din `GitHubOptions`; credențialele vin din `IGitHubConfigurationService`; nu hardcodați identificatori sau secrete. Adresele GitHub.com sunt valori implicite care se pot schimba din configurație.
- Clientul HTTP nu are încă teste automate (proiectul de teste referă numai Business); o schimbare se verifică cu un handler HTTP simulat ([docs/TESTING.md](../../docs/TESTING.md)).

- Credențialele, scopes și callback URL vin asincron din `IGitHubConfigurationService` la fiecare operație OAuth; `GitHubOptions` păstrează numai endpoint-uri și timeout. Nu memorați credentialele și nu calculați callback-ul în Web.
