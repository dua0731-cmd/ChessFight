param(
    [string]$Copy = 'C:\유니티\ChessFight_TestCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [switch]$NoCapture
)
# R834: the first-run tutorial grey box's checks on a copy of this project (Run-GrandBanquetHall.ps1's way), so the
# Editor can keep the original open. Brings the copy up to date with this folder (never the copy's own Library), then
# runs TutorialValidation.RunBatch there in batch mode with a graphics device: builds Assets/Scenes/Tutorial.unity in
# the copy, then plays it and moves the pawn through every lesson (no keys pressed). -NoCapture: no pictures.
# Results: <copy>/Logs/TutorialValidation (tutorial.txt, *.png), Unity's log <copy>/Logs/tutorial_unity.log.
# The scene built in the copy stays there: build it in the real project with ChessFight > Development > Tutorial.
#   ./Tools/Run-Tutorial.ps1
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
$unityLog = Join-Path $logs 'tutorial_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.RagdollLab.Editor.TutorialValidation.RunBatch')
if ($NoCapture) { $arguments += '-tutorialNoCapture' }
$arguments += @('-logFile', "`"$unityLog`"")
Write-Host 'Running TutorialValidation.RunBatch in Unity (batch mode)...'
$started = Get-Date
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
$report = Join-Path $logs 'TutorialValidation/tutorial.txt'
if ((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $started) {
    Get-Content -LiteralPath $report -Encoding UTF8
} else {
    Write-Host "No report from this run at $report; the end of Unity's log:"
    if (Test-Path -LiteralPath $unityLog) { Get-Content -LiteralPath $unityLog -Tail 40 -Encoding UTF8 }
}
Write-Host "Unity exit code $($unity.ExitCode); results in $(Join-Path $logs 'TutorialValidation'), Unity's log $unityLog"
exit $unity.ExitCode
