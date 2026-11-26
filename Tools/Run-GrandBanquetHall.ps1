param(
    [string]$Copy = 'C:\유니티\ChessFight_TestCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [switch]$NoCapture,
    # R845: only the held-clock pictures whose names start so (e.g. 'stage'), none of the hall's or the table's
    [string]$Only = ''
)
# R816: the grand banquet hall's (대연회장) grey-box checks on a copy of this project, so the Editor can keep the original
# open (Run-RoyalClockTower.ps1's way). Brings the copy up to date with this folder (never the copy's own Library), then
# runs GrandBanquetHallValidation.RunBatch there in batch mode with a graphics device: the Edit checks (the section checks,
# what is there, which faces can be held, the waltz's arithmetic) and then 14 s of Play (every waiter, cloche, couple, pleat
# and chandelier read, the tablecloth's pulls and the tower's overflows counted, errors, pictures). -NoCapture: no
# pictures. Results: <copy>/Logs/GrandBanquetHallValidation (hall.txt, *.png), Unity's log
# <copy>/Logs/grandbanquethall_unity.log. Not a play test with people and not online.
#   ./Tools/Run-GrandBanquetHall.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$lock = Join-Path $Copy 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lock) {
    try { [System.IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Dispose() }
    catch { throw "A Unity is still open on the copy ($lock is locked): close it or end the leftover Unity.exe, then run again." }
}
$skipDirs = @('.git', 'Temp', 'Logs', 'obj', '.vs', '.vscode')
$skipFiles = @('*.csproj', '*.sln', '*.slnx')
if (!(Test-Path -LiteralPath (Join-Path $Copy 'Library'))) {
    Write-Host "Making the copy (first time, with the Library): $Copy"
    robocopy $root $Copy /MIR /XD @skipDirs /XF @skipFiles /R:1 /W:1 /MT:16 /NP /NFL /NDL | Out-Null
} else {
    Write-Host "Bringing the copy up to date: $Copy"
    robocopy $root $Copy /MIR /XD @($skipDirs + 'Library') /XF @skipFiles /R:1 /W:1 /MT:16 /NP /NFL /NDL | Out-Null
}
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
# PITFALLS #53: a Library copied from the real project keeps that editor's process files.
foreach ($f in 'ilpp.pid', 'EditorInstance.json', 'ArtifactDB-lock', 'SourceAssetDB-lock') {
    Remove-Item -LiteralPath (Join-Path $Copy "Library/$f") -ErrorAction SilentlyContinue
}
$logs = Join-Path $Copy 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$unityLog = Join-Path $logs 'grandbanquethall_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.GrandBanquetHallValidation.RunBatch')
if ($NoCapture) { $arguments += '-banquetNoCapture' }
if ($Only) { $arguments += @('-banquetOnly', $Only) }
$arguments += @('-logFile', "`"$unityLog`"")
Write-Host 'Running GrandBanquetHallValidation.RunBatch in Unity (batch mode)...'
$started = Get-Date
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
$report = Join-Path $logs 'GrandBanquetHallValidation/hall.txt'
if ((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $started) {
    Get-Content -LiteralPath $report -Encoding UTF8
} else {
    Write-Host "No report from this run at $report; the end of Unity's log:"
    if (Test-Path -LiteralPath $unityLog) { Get-Content -LiteralPath $unityLog -Tail 40 -Encoding UTF8 }
}
Write-Host "Unity exit code $($unity.ExitCode); results in $(Join-Path $logs 'GrandBanquetHallValidation'), Unity's log $unityLog"
exit $unity.ExitCode
