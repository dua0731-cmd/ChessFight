# R736: pictures of the lobby's 기물 도감 and 전적 tabs on the project copy, so the open Editor is left alone (the AI's own
# look at the layout, not a user check). Brings the copy up to date first as Run-ZoneLightCheck.ps1 does, then runs
# LobbyTabsCapture.RunPlayBatch in batch Unity: the lobby stage and HUD with no Steam, a made-up party and record book.
# Results: <copy>/Logs/LobbyTabsCapture (1_play.png ... 7_record_empty.png, lobby_tabs.txt).
# R749: -Language zh-Hans (or en, any column of the string table) takes the pictures in that language.
param([string]$Language = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$Copy = 'C:\유니티\ChessFight_TestCopy'
$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor'
$skipDirs = @('.git', 'Temp', 'Logs', 'obj', '.vs', '.vscode', 'Library', 'UserSettings')
$skipFiles = @('*.csproj', '*.sln', '*.slnx', '*.blend', '*.blend1')
Write-Host "Bringing the copy up to date: $Copy"
robocopy $root $Copy /MIR /XD @skipDirs /XF @skipFiles /R:1 /W:1 /MT:16 /NP /NFL /NDL | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
foreach ($f in 'ilpp.pid', 'EditorInstance.json', 'ArtifactDB-lock', 'SourceAssetDB-lock') {
    Remove-Item -LiteralPath (Join-Path $Copy "Library/$f") -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath (Join-Path $Copy 'Temp/UnityLockfile') -ErrorAction SilentlyContinue
$logs = Join-Path $Copy 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Remove-Item -LiteralPath (Join-Path $logs 'LobbyTabsCapture') -Recurse -ErrorAction SilentlyContinue
$unityLog = Join-Path $logs 'lobby_tabs_capture_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.LobbyTabsCapture.RunPlayBatch', '-logFile', "`"$unityLog`"")
if ($Language) { $arguments += @('-captureLanguage', $Language) }
Write-Host 'Capturing the lobby tabs in Unity (batch mode, Play)...'
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Write-Host "Unity exit code $($unity.ExitCode)"
Get-Content -LiteralPath (Join-Path $logs 'LobbyTabsCapture/lobby_tabs.txt') -ErrorAction SilentlyContinue
exit $unity.ExitCode
