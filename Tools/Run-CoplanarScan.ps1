param(
    [string]$Copy = 'C:\유니티\ChessFight_TestCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    # write Assets/Resources/{BanquetHall,RoyalLibrary}/coplanar_nudges.txt (Gameplay CoplanarNudges) and bring them back here
    [switch]$Bake
)
# R904: CoplanarScan.RunBatch on a copy of this project (Run-RookRampart.ps1's way): the faces that flicker (z-fighting)
# in the grand banquet hall and the royal library as drawn in Play. Results: <copy>/Logs/CoplanarScan/<prefab>.txt (what is
# left: between two objects, inside one FBX). -Bake: first writes the nudges for faces of two objects (CoplanarScan's
# header) and copies them back to this folder. Faces inside one FBX are lifted on import (CoplanarLiftImporter).
#   ./Tools/Run-CoplanarScan.ps1 [-Bake]
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
$unityLog = Join-Path $logs 'coplanar_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.CoplanarScan.RunBatch',
               '-logFile', "`"$unityLog`"")
if ($Bake) { $arguments += '-coplanarBake' }
Write-Host 'Running CoplanarScan.RunBatch in Unity (batch mode)...'
$started = Get-Date
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
foreach ($name in 'Mid_2-4_GrandBanquetHall', 'Late_3-1_RoyalLibrary') {
    $report = Join-Path $logs "CoplanarScan/$name.txt"
    if ((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $started) {
        Get-Content -LiteralPath $report -Encoding UTF8 | Where-Object { $_ -notmatch '^   ' }
    } else {
        Write-Host "No report from this run at $report"
    }
}
$errFile = Join-Path $logs 'CoplanarScan/error.txt'
if (Test-Path -LiteralPath $errFile) { Get-Content -LiteralPath $errFile -Encoding UTF8 }
if ($Bake) {
    foreach ($folder in 'BanquetHall', 'RoyalLibrary') {
        $made = Join-Path $Copy "Assets/Resources/$folder/coplanar_nudges.txt"
        if ((Test-Path -LiteralPath $made) -and (Get-Item -LiteralPath $made).LastWriteTime -ge $started) {
            Copy-Item -LiteralPath $made -Destination (Join-Path $root "Assets/Resources/$folder/coplanar_nudges.txt") -Force
            Write-Host "Brought back $folder/coplanar_nudges.txt"
        }
    }
}
Write-Host "Unity exit code $($unity.ExitCode); results in $(Join-Path $logs 'CoplanarScan'), Unity's log $unityLog"
exit $unity.ExitCode
