$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$resourceFolder = Join-Path $repoRoot 'WorkNotes.Resources/Resources'
$catalogs = @{}
foreach ($suffix in @('', '.ro', '.en', '.pl')) {
    [xml]$xml = Get-Content -LiteralPath (Join-Path $resourceFolder "SharedResources$suffix.resx") -Raw
    $entries = @{}
    foreach ($entry in $xml.root.data) {
        if ($entries.ContainsKey($entry.name)) { throw "Duplicate key: $($entry.name)" }
        if ([string]::IsNullOrWhiteSpace($entry.value)) { throw "Empty translation: $suffix / $($entry.name)" }
        $entries[$entry.name] = [string]$entry.value
    }
    $catalogs[$suffix] = $entries
}
$neutral = $catalogs['']
foreach ($suffix in @('.ro', '.en', '.pl')) {
    if (Compare-Object @($neutral.Keys | Sort-Object) @($catalogs[$suffix].Keys | Sort-Object)) {
        throw "Mismatched keys: $suffix"
    }
    foreach ($key in $neutral.Keys) {
        $expected = @([regex]::Matches($neutral[$key], '\{\d+\}') | ForEach-Object Value | Sort-Object)
        $actual = @([regex]::Matches($catalogs[$suffix][$key], '\{\d+\}') | ForEach-Object Value | Sort-Object)
        if (($expected -join ',') -ne ($actual -join ',')) { throw "Mismatched placeholders: $suffix / $key" }
        if ($suffix -eq '.ro' -and $neutral[$key] -cne $catalogs[$suffix][$key]) { throw "Romanian fallback differs: $key" }
    }
}
$source = foreach ($project in @('WorkNotes.Web','WorkNotes.Business','WorkNotes.DataAccess')) {
    Get-ChildItem (Join-Path $repoRoot $project) -Recurse -File |
        Where-Object { $_.Extension -in '.cs','.cshtml','.js' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
}
$source = $source -join "`n"
foreach ($key in $neutral.Keys) {
    if (-not $source.Contains('"' + $key + '"')) { throw "Unused resource key: $key" }
}
foreach ($match in [regex]::Matches($source, '"((?:Navigation|Field|Button|Language|Home|Dashboard|Footer|Validation|Message|Identity)_[A-Za-z0-9]+)"')) {
    if (-not $neutral.ContainsKey($match.Groups[1].Value)) { throw "Missing resource key: $($match.Groups[1].Value)" }
}
Write-Output "PASS: $($neutral.Count) keys; Romanian fallback; ro/en/pl parity; placeholders; no missing, empty or unused keys."
