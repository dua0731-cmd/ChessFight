param(
    [Parameter(Mandatory = $true)][ValidateSet('ValidateEdit', 'Validate', 'Bots')][string]$What,
    [string]$Copy = 'C:\유니티\ChessFight_TestCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [ValidateSet('walk', 'fork1', 'hammers', 'fork2safe', 'fork2risky', 'plates', 'door', 'full')][string]$Beat = 'full',
    [ValidateRange(1, 10)][int]$Runs = 4,
    [int]$Seed = 7,
    [ValidateSet('pawn', 'mix')][string]$Pieces = 'pawn',
    [int]$RoundSeed = 0,
    [switch]$NoCapture
)
# R725: the royal vault's (왕실 금고실) checks on a copy of this project, so the Editor can keep the original open. Brings the
# copy up to date with this folder (robocopy: the first time everything with the Library, later only what changed and never
# the copy's own Library), then runs one of them in batch mode there with a graphics device (pictures):
#   -What ValidateEdit  RoyalVaultValidation.RunEditBatch: both prefabs, the vault built in Edit mode, E1-E15 and the
#                       section checks (SectionValidator)
#   -What Validate      RoyalVaultValidation.RunPlayBatch: the vault alone in Play Mode, the clock frozen at chosen times,
#                       P1-P10 (-RoundSeed N: a whole round instead, which must draw the vault in slot 5 - only after the
#                       pool step)
#   -What Bots          RoyalVaultBotTest.RunPlayBatch: twelve ragdoll pawns, -Beat walk|fork1|hammers|fork2safe|fork2risky|
#                       plates|door|full, -Runs 1..10, -Seed, -Pieces pawn|mix
# -NoCapture: no pictures. Both batch methods exit by themselves (never -quit). Results: <copy>/Logs/RoyalVaultValidation
# (vault.txt, the last run; vault_edit.txt and vault_play.txt each mode's last; *.png) or <copy>/Logs/RoyalVaultBots
# (report.txt, *.csv, *.png), Unity's log <copy>/Logs/royalvault_unity.log. The bots' exit code is 1 only for Play errors and
# harness failures: read report.txt. Rename the results folder between experiments to keep one.
# Not a play test with people and not online.
#   ./Tools/Run-RoyalVault.ps1 -What ValidateEdit
#   ./Tools/Run-RoyalVault.ps1 -What Validate
#   ./Tools/Run-RoyalVault.ps1 -What Bots -Beat walk -Runs 2 -Seed 7 -NoCapture
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# A Unity still open on the copy (the Editor, or a batch run that never exited) holds its lock file and would refuse this
# run, or the copy would change under it: stop here instead.
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
# PITFALLS #53 (R719): a Library copied from the real project keeps that editor's process files, and the copy's Unity
# would take its IL post-processing runner for a leftover and kill it. They are the open editor's, never the copy's.
foreach ($f in 'ilpp.pid', 'EditorInstance.json', 'ArtifactDB-lock', 'SourceAssetDB-lock') {
    Remove-Item -LiteralPath (Join-Path $Copy "Library/$f") -ErrorAction SilentlyContinue
}
$logs = Join-Path $Copy 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$unityLog = Join-Path $logs 'royalvault_unity.log'
switch ($What) {
    'ValidateEdit' {
        $method = 'RoyalVaultValidation.RunEditBatch'
        $results = 'RoyalVaultValidation'
        $reportName = 'vault.txt'
        $extra = @()
    }
    'Validate' {
        $method = 'RoyalVaultValidation.RunPlayBatch'
        $results = 'RoyalVaultValidation'
        $reportName = 'vault.txt'
        $extra = @()
        if ($RoundSeed -ne 0) { $extra += @('-vaultRoundSeed', $RoundSeed) }
        if ($NoCapture) { $extra += '-vaultNoCapture' }
    }
    'Bots' {
        $method = 'RoyalVaultBotTest.RunPlayBatch'
        $results = 'RoyalVaultBots'
        $reportName = 'report.txt'
        $extra = @('-vaultBotRuns', $Runs, '-vaultBotSeed', $Seed, '-vaultBotBeat', $Beat, '-vaultBotPieces', $Pieces)
        if ($NoCapture) { $extra += '-vaultBotNoCapture' }
    }
}
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', "ChessFight.PawnRush.Editor.$method") + $extra +
             @('-logFile', "`"$unityLog`"")
Write-Host "Running $method in Unity (batch mode)..."
$started = Get-Date
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
$report = Join-Path $logs "$results/$reportName"
# Only this run's report: a run that never got to its checks (a compile error, say) leaves the last one standing.
if ((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $started) {
    Get-Content -LiteralPath $report -Tail 40 -Encoding UTF8
} else {
    Write-Host "No report from this run at $report; the end of Unity's log:"
    if (Test-Path -LiteralPath $unityLog) { Get-Content -LiteralPath $unityLog -Tail 40 -Encoding UTF8 }
}
Write-Host "Unity exit code $($unity.ExitCode); results in $(Join-Path $logs $results), Unity's log $unityLog"
exit $unity.ExitCode
