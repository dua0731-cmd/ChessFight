# R747: what the string table (Assets/Resources/Localization/Strings.txt) is still missing.
#
# Finds every Korean key the game asks for - the first literal of each Loc.T("...") / Loc.F("...") in
# Assets/Scripts, and each Korean text="..." in Assets/Resources/*.uxml (RuntimePanels.Create translates those) -
# and lists, per language, the keys with no line in the table or an empty cell. Text that reaches Loc.T through a
# variable (the settings window's names) is not seen here; it shows as "in the table, not seen in code".
#
#   ./Tools/Localization/Find-Untranslated.ps1                 every language column, counts and the missing keys
#   ./Tools/Localization/Find-Untranslated.ps1 -Language en    one language
#   ./Tools/Localization/Find-Untranslated.ps1 -AddMissing     also appends the keys with no line yet, cells empty
param(
    [string]$Language = '',
    [switch]$AddMissing,
    [switch]$ShowUnused
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$tablePath = Join-Path $root 'Assets/Resources/Localization/Strings.txt'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Unescape-Cell([string]$s) {
    # Windows PowerShell 5.1 has no `u{...}: a NUL stands in for an escaped backslash while the others are read.
    $z = [string][char]0
    $s.Replace('\\', $z).Replace('\n', "`n").Replace('\t', "`t").Replace($z, '\')
}
function Escape-Cell([string]$s) {
    $s.Replace('\', '\\').Replace("`r`n", "`n").Replace("`n", '\n').Replace("`t", '\t')
}
function Unescape-CSharp([string]$s) {
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $s.Length; $i++) {
        $c = $s[$i]
        if ($c -eq '\' -and $i + 1 -lt $s.Length) {
            $e = $s[$i + 1]; $i++
            switch -CaseSensitive ($e) {
                'n' { [void]$sb.Append("`n") }
                't' { [void]$sb.Append("`t") }
                'r' { [void]$sb.Append("`r") }
                '"' { [void]$sb.Append('"') }
                "'" { [void]$sb.Append("'") }
                '\' { [void]$sb.Append('\') }
                '0' { [void]$sb.Append([char]0) }
                default { [void]$sb.Append('\').Append($e) }
            }
        } else { [void]$sb.Append($c) }
    }
    $sb.ToString()
}

# ---------- the table ----------
$lines = [System.IO.File]::ReadAllLines($tablePath, $utf8)
$codes = $null
$rows = @{}
foreach ($line in $lines) {
    $l = $line.TrimStart([char]0xFEFF)
    if ($l.Trim().Length -eq 0 -or $l.StartsWith('#')) { continue }
    $cells = $l.Split("`t")
    if ($null -eq $codes) { $codes = $cells | ForEach-Object { $_.Trim() }; continue }
    if ($cells[0] -eq '@ready') { continue }
    $rows[(Unescape-Cell $cells[0])] = $cells
}
if ($null -eq $codes -or $codes[0] -ne 'ko') { throw "The table's first line must be language codes starting with ko: $tablePath" }
$languages = @($codes | Select-Object -Skip 1)
if ($Language) {
    if ($languages -notcontains $Language) { throw "The table has no column '$Language' (it has: $($languages -join ', '))." }
    $languages = @($Language)
}

# ---------- what the game asks for ----------
$hangul = "[" + [char]0xAC00 + "-" + [char]0xD7A3 + "]"   # Hangul syllables (kept ASCII: Windows PowerShell reads this file as ANSI)
$wanted = [ordered]@{}   # key -> first place it is used
$callPattern = 'Loc\.(?:T|F)\(\s*"((?:[^"\\]|\\.)*)"'
Get-ChildItem (Join-Path $root 'Assets/Scripts') -Recurse -Filter '*.cs' | ForEach-Object {
    $file = $_.FullName.Substring($root.Length + 1).Replace('\', '/')
    $text = [System.IO.File]::ReadAllText($_.FullName, $utf8)
    foreach ($m in [regex]::Matches($text, $callPattern)) {
        $key = Unescape-CSharp $m.Groups[1].Value
        if ($key -notmatch $hangul) { continue }
        # A call written in a comment (an example in the docs) is not one the game makes.
        $lineStart = $text.LastIndexOf("`n", [Math]::Max(0, $m.Index - 1)) + 1
        if ($text.Substring($lineStart, $m.Index - $lineStart).Contains('//')) { continue }
        $lineNo = ($text.Substring(0, $m.Index) -split "`n").Count
        if (-not $wanted.Contains($key)) { $wanted[$key] = "${file}:$lineNo" }
    }
}
Get-ChildItem (Join-Path $root 'Assets/Resources') -Recurse -Filter '*.uxml' | ForEach-Object {
    $file = $_.FullName.Substring($root.Length + 1).Replace('\', '/')
    $text = [System.IO.File]::ReadAllText($_.FullName, $utf8)
    foreach ($m in [regex]::Matches($text, '\btext="([^"]*)"')) {
        $key = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value)
        if ($key -notmatch $hangul) { continue }
        $lineNo = ($text.Substring(0, $m.Index) -split "`n").Count
        if (-not $wanted.Contains($key)) { $wanted[$key] = "${file}:$lineNo" }
    }
}

# ---------- the report ----------
$noLine = @($wanted.Keys | Where-Object { -not $rows.ContainsKey($_) })
Write-Output ("Keys the game asks for: {0} ({1} have no line in the table). Table lines: {2}." -f $wanted.Count, $noLine.Count, $rows.Count)
foreach ($lang in $languages) {
    $col = [array]::IndexOf($codes, $lang)
    $missing = @($wanted.Keys | Where-Object { -not $rows.ContainsKey($_) -or $rows[$_].Length -le $col -or $rows[$_][$col].Length -eq 0 })
    Write-Output ''
    Write-Output ("[{0}] translated {1} / {2}, missing {3}" -f $lang, ($wanted.Count - $missing.Count), $wanted.Count, $missing.Count)
    foreach ($key in $missing) { Write-Output ("  {0}  {1}" -f $wanted[$key], (Escape-Cell $key)) }
}
if ($ShowUnused) {
    $unused = @($rows.Keys | Where-Object { -not $wanted.Contains($_) })
    Write-Output ''
    Write-Output ("In the table, not seen in code ({0}) - fine for text that reaches Loc.T through a variable:" -f $unused.Count)
    foreach ($key in $unused) { Write-Output ("  " + (Escape-Cell $key)) }
}
if ($AddMissing -and $noLine.Count -gt 0) {
    $tabs = "`t" * ($codes.Count - 1)
    $add = New-Object System.Collections.Generic.List[string]
    $add.Add('# --- found by Find-Untranslated.ps1 (' + (Get-Date -Format 'yyyy-MM-dd') + ')')
    foreach ($key in $noLine) { $add.Add((Escape-Cell $key) + $tabs) }
    $existing = [System.IO.File]::ReadAllText($tablePath, $utf8)
    $sep = if ($existing.EndsWith("`n")) { '' } else { "`n" }
    [System.IO.File]::AppendAllText($tablePath, $sep + ($add -join "`n") + "`n", $utf8)
    Write-Output ''
    Write-Output ("Appended {0} keys with empty cells to {1}." -f $noLine.Count, $tablePath)
}
