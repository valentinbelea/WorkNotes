# Fișiere temporare de operator

`InsertGitHubConfiguration.sql` este un șablon temporar cerut pentru pregătirea configurației OAuth. Nu face parte din ordinea scripturilor de versiune și nu se aplică automat.

Șablonul nu acceptă credențiale OAuth în clar. Variabilele `@ProtectedClientId` și `@ProtectedClientSecret` trebuie să fie payload-uri ASP.NET Core Data Protection create de instanța WorkNotes țintă cu purpose string-ul `WorkNotes.GitHubConfiguration.v1`. În lipsa lor, scriptul se oprește înainte de orice modificare.

Fluxul recomandat rămâne salvarea prin `/admin/configuration`, care produce payload-urile cu key ring-ul corect și creează sau actualizează rândul mediului selectat. Nu salvați în acest folder Client secret, chei private, fișiere PEM sau tokenuri.
