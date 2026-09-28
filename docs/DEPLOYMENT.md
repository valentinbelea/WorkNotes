# Medii și publicare

Documentul conține numai ce se poate confirma din cod și din documentele existente; restul este marcat „TODO: Necesită clarificare”. Scripturile SQL se aplică numai la cerere explicită ([AGENTS.md](../AGENTS.md#ef-core-și-schema-sql-database-first)).

## Development

- Confirmat: profilurile din `Solution/WorkNotes.Web/Properties/launchSettings.json`, toate cu `ASPNETCORE_ENVIRONMENT=Development`:
  - `http` — http://localhost:5018 (`dotnet run --project WorkNotes.Web --launch-profile http`);
  - `https` — https://localhost:7190 și http://localhost:5018;
  - `IIS Express` — http://localhost:57690, port SSL 44389, autentificare anonimă.
- Baza: instanța locală `localhost\MSSQLSERVER02`, baza `WorkNotes.db`, Windows Authentication, `TrustServerCertificate=True` (configurația din `appsettings.json`).
- În Development: pagina de excepții pentru dezvoltatori, fără HSTS și fără redirecționare HTTPS; cookie-ul de autentificare este `Secure` numai pe HTTPS (`SameAsRequest`).

## Test

TODO: Necesită clarificare — nu există informații despre un mediu Test (server, bază, adresă, configurare, cine are acces).

## Production

TODO: Necesită clarificare — nu există informații despre mediul Production (hosting, server, bază, domeniu, certificat, conturi de serviciu).

Ce impune codul în afara Development (orice mediu care nu este `Development`):

- `UseExceptionHandler()` cu ProblemDetails, `UseHsts()` și `UseHttpsRedirection()`;
- cookie-ul de autentificare `WorkNotes.Auth` numai prin HTTPS (`CookieSecurePolicy.Always`); HTTPS este obligatoriu pentru găzduire.

## IIS

- Singura configurare IIS existentă este profilul IIS Express pentru dezvoltare. Repository-ul nu conține `web.config` (SDK-ul îl generează la publicare) și nicio configurație de server IIS.
- Conexiunea cu Windows Authentication folosește identitatea procesului; sub IIS aceasta ar fi identitatea pool-ului de aplicații, care ar avea nevoie de drepturi de citire și scriere în baza aplicației.
- TODO: Necesită clarificare — dacă aplicația se găzduiește în IIS, modelul de găzduire, pool-ul și identitatea lui, versiunea ASP.NET Core Hosting Bundle.

## Hosting

Cerințe confirmate de documentele existente:

- HTTPS;
- configurarea conexiunii din mediu, nu din fișiere versionate;
- chei Data Protection persistente, protejate și comune instanțelor, dacă sunt mai multe (aplicația nu configurează încă Data Protection — [SECURITY.md](SECURITY.md#secretele)).

TODO: Necesită clarificare — furnizorul sau serverul de găzduire, numărul de instanțe, lista `AllowedHosts`.

## Configurare

| Cheie | Rol | Observații |
| --- | --- | --- |
| `ConnectionStrings:WorkNotes` | Conexiunea SQL Server | Obligatorie: aplicația nu pornește fără ea; pentru alte medii se furnizează prin configurația mediului, de exemplu variabila `ConnectionStrings__WorkNotes` |
| `Logging:LogLevel` | Nivelurile de log | `Default = Information`, `Microsoft.AspNetCore = Warning` |
| `AllowedHosts` | Host-urile acceptate | `*` în configurația versionată |
| `ASPNETCORE_ENVIRONMENT` | Mediul | `Development` în profilurile locale |

Fișierele: `appsettings.json` (comun), `appsettings.Development.json` (numai logging). Credențialele nu se pun în fișiere versionate.

## Publicare

- Nu există o procedură de publicare documentată, un pipeline CI/CD sau un profil de publicare în repository.
- Comanda standard .NET, verificată în sesiunea de documentare din 2026-09-25 (pe Linux), care produce aplicația fără fișierele de documentație:

```powershell
dotnet publish WorkNotes.Web/WorkNotes.Web.csproj -c Release -o <folder-publicare>
```

- TODO: Necesită clarificare — procedura oficială de publicare, destinația și cine o execută.

## Scripturile SQL la publicare

1. Se stabilește ce scripturi lipsesc din baza țintă, comparând `dbo.DatabaseVersion` și obiectele existente cu [Scripts/README.md](../Scripts/README.md).
2. Se aplică scripturile lipsă, în ordine, cu o identitate care are drepturi de modificare a schemei, înainte de pornirea noii versiuni a aplicației (codul presupune schema versiunii lui).
3. Scripturile conțin `USE [WorkNotes.db];`: pe o bază cu alt nume trebuie adaptată execuția. TODO: Necesită clarificare — numele bazelor din Test și Production.

## Backup

TODO: Necesită clarificare — nu există o procedură de backup documentată (frecvență, locul copiilor, backup înainte de aplicarea scripturilor, testarea restaurării).

## Verificări după publicare

1. Aplicația pornește fără erori, pe HTTPS.
2. Footerul afișează versiunea așteptată (în prezent `v.0.02`), nu „Versiune neconfigurată”.
3. Autentificarea funcționează; cookie-ul `WorkNotes.Auth` este `Secure` și HttpOnly.
4. Tabla unui context se încarcă, iar o notă se deschide în editor.
5. Schimbarea limbii funcționează în română, engleză și poloneză.
6. Logurile nu conțin erori la pornire.

## Rollback

- Scripturile SQL sunt numai înainte; nu există scripturi de rollback, iar rândurile din `DatabaseVersion` nu se șterg ([VERSIONING.md](VERSIONING.md#rollback)).
- TODO: Necesită clarificare — procedura de rollback a aplicației (revenirea la publicarea anterioară) și a bazei (de exemplu restaurarea unui backup).
