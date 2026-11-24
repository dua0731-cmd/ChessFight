# R725: the Royal Observatory's graybox check and performance measure (RoyalObservatoryValidation.RunPlayBatch, then
# RoyalObservatoryPerf) on the project copy, so the open Editor is left alone. Brings the copy up to date first (robocopy,
# never the copy's Library or .blend files), removes the lock files a copied Library carries (PITFALLS 53).
# Results: <copy>/Logs/RoyalObservatory (report.txt, perf.txt, perf_views.csv, pictures).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$Copy = 'C:\유니티\ChessFight_TestCopy'
$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor'
$skipDirs = @('.git', 'Temp', 'Logs', 'obj', '.vs', '.vscode', 'Library', 'UserSettings')   # R727: UserSettings is not in git - a worktree has none, and mirroring it away breaks the copy's Editor search (an exception in Play)
$skipFiles = @('*.csproj', '*.sln', '*.slnx', '*.blend', '*.blend1')
Write-Host "Bringing the copy up to date: $Copy"
robocopy $root $Copy /MIR /XD @skipDirs /XF @skipFiles /R:1 /W:1 /MT:16 /NP /NFL /NDL | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
foreach ($f in 'ilpp.pid', 'EditorInstance.json', 'ArtifactDB-lock', 'SourceAssetDB-lock') {
    Remove-Item -LiteralPath (Join-Path $Copy "Library/$f") -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath (Join-Path $Copy 'Temp/UnityLockfile') -ErrorAction SilentlyContinue
# The copy is not a seoungtest editor session: its observatory-only switch must not reopen scenes in batch (it skips batch anyway).
$logs = Join-Path $Copy 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Remove-Item -LiteralPath (Join-Path $logs 'RoyalObservatory') -Recurse -ErrorAction SilentlyContinue
$unityLog = Join-Path $logs 'royal_observatory_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.RoyalObservatoryValidation.RunPlayBatch', '-logFile', "`"$unityLog`"")
Write-Host "Running the Royal Observatory check in Unity (batch mode)..."
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Write-Host "Unity exit code $($unity.ExitCode)"
Get-Content -LiteralPath (Join-Path $logs 'RoyalObservatory/report.txt') -ErrorAction SilentlyContinue
Get-Content -LiteralPath (Join-Path $logs 'RoyalObservatory/perf.txt') -ErrorAction SilentlyContinue
exit $unity.ExitCode
