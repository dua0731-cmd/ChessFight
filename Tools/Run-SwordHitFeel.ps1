param(
    [string]$Copy = 'C:\유니티\ChessFight_TestCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor'
)
# R808: the Sword Fight click cut's hit feel (SwordHitFeelCheck → SwordHitFeelProbe) on a copy of this project, so the
# Editor can keep the original open. Brings the copy up to date with this folder (robocopy: the first time everything with
# the Library, later only what changed and never the copy's own Library), then plays the match scene alone in batch mode
# there with a graphics device (pictures). It measures every piece's windup and whole cut, a queen's combo beat, a hit's
# catch and hold, the cutter found, the hit mark, the sounds' levels, the protection shell and a client's timing (made-up
# host frames fed to two puppets). Results: <copy>/Logs/SwordHitFeel (report.txt, *.png), Unity's log
# <copy>/Logs/swordhitfeel_unity.log. Exit code 0 only when every check passed. Not a play test with people, not online.
#   ./Tools/Run-SwordHitFeel.ps1
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
$unityLog = Join-Path $logs 'swordhitfeel_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.RagdollLab.Editor.SwordHitFeelCheck.RunPlayBatch',
               '-logFile', "`"$unityLog`"")
Write-Host "Running SwordHitFeelCheck.RunPlayBatch in Unity (batch mode)..."
$started = Get-Date
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
$report = Join-Path $logs 'SwordHitFeel/report.txt'
# Only this run's report: a run that never got to its checks (a compile error, say) leaves the last one standing.
if ((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $started) {
    Get-Content -LiteralPath $report -Encoding UTF8
} else {
    Write-Host "No report from this run at $report; the end of Unity's log:"
    if (Test-Path -LiteralPath $unityLog) { Get-Content -LiteralPath $unityLog -Tail 60 -Encoding UTF8 }
}
Write-Host "Unity exit code $($unity.ExitCode); results in $(Join-Path $logs 'SwordHitFeel'), Unity's log $unityLog"
exit $unity.ExitCode
