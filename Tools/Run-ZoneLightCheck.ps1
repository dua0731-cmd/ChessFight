# R734: the places' light mixer (Game ZoneLight) checked on the project copy, so the open Editor is left alone. Brings the
# copy up to date first as Run-RoyalObservatoryCheck.ps1 does, then runs two batch Unity sessions:
#   ZoneLightCheck.RunBatch      an empty scene, a white sphere lit only by the ambient light (report.txt)
#   ZoneLightWalk.RunPlayBatch   Play: a camera running through two places, every frame's light change (walk.txt)
# Results: <copy>/Logs/ZoneLightCheck/.
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
Remove-Item -LiteralPath (Join-Path $logs 'ZoneLightCheck') -Recurse -ErrorAction SilentlyContinue
$code = 0
foreach ($run in @(
    @{ Method = 'ChessFight.PawnRush.Editor.ZoneLightCheck.RunBatch'; Log = 'zone_light_check_unity.log'; Report = 'report.txt'; Say = 'the ambient blend' },
    @{ Method = 'ChessFight.PawnRush.Editor.ZoneLightWalk.RunPlayBatch'; Log = 'zone_light_walk_unity.log'; Report = 'walk.txt'; Say = 'the walk through two places (Play)' })) {
    $unityLog = Join-Path $logs $run.Log
    $arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', $run.Method, '-logFile', "`"$unityLog`"")
    Write-Host "Checking $($run.Say) in Unity (batch mode)..."
    $unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
    Write-Host "Unity exit code $($unity.ExitCode)"
    Get-Content -LiteralPath (Join-Path $logs "ZoneLightCheck/$($run.Report)") -ErrorAction SilentlyContinue
    if ($unity.ExitCode -ne 0) { $code = $unity.ExitCode }
}
exit $code
