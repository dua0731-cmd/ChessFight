# R733: pictures of the garden's and the grand stair hall's checkpoint triggers (CheckpointBoxCapture.RunPlayBatch) on the
# project copy, so the open Editor is left alone. Brings the copy up to date first as Run-RoyalObservatoryCheck.ps1 does.
# Results: <copy>/Logs/CheckpointBoxes (report.txt, pictures).
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
Remove-Item -LiteralPath (Join-Path $logs 'CheckpointBoxes') -Recurse -ErrorAction SilentlyContinue
$unityLog = Join-Path $logs 'checkpoint_boxes_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.CheckpointBoxCapture.RunPlayBatch', '-logFile', "`"$unityLog`"")
Write-Host "Taking the checkpoint pictures in Unity (batch mode)..."
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Write-Host "Unity exit code $($unity.ExitCode)"
Get-Content -LiteralPath (Join-Path $logs 'CheckpointBoxes/report.txt') -ErrorAction SilentlyContinue
exit $unity.ExitCode
