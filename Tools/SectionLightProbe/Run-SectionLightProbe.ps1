param(
    [string]$Copy = 'C:\유니티\ChessFight_TestCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [string]$Only = ''
)
# R809: the section light probe (Tools/SectionLightProbe/SectionLightProbe.cs) on a copy of this project, so the open
# Editor keeps the original: the carriage yard, the grand stair hall, the war room, the audience hall, the moat drawbridge
# and the palace laundry built one at a time in Play - their lights (shadow-casting ones by name), renderers, and a
# pawn's view at every beat drawn into 1920x1080. Brings the copy up to date as Tools/GrdProbe/Run-GrdProbe.ps1 does
# (never the copy's own Library), puts the probe into <copy>/Assets/SectionLightProbe/Editor with its own editor
# assembly and runs it in batch mode with a graphics device. Results: <copy>/Logs/SectionLightProbe (report.txt,
# lights.csv, views.csv). Times are the Editor's batch mode on one thread - for comparing sections, not a frame rate.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$skipDirs = @('.git', 'Temp', 'Logs', 'obj', '.vs', '.vscode', 'Library', 'UserSettings')
$skipFiles = @('*.csproj', '*.sln', '*.slnx', '*.blend', '*.blend1')
Write-Host "Bringing the copy up to date: $Copy"
# /E, not /MIR: the copy is shared with other sessions' probes (their files under the copy's Assets stay).
robocopy $root $Copy /E /XD @skipDirs /XF @skipFiles /R:1 /W:1 /MT:16 /NP /NFL /NDL | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
foreach ($f in 'ilpp.pid', 'EditorInstance.json', 'ArtifactDB-lock', 'SourceAssetDB-lock') {
    Remove-Item -LiteralPath (Join-Path $Copy "Library/$f") -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath (Join-Path $Copy 'Temp/UnityLockfile') -ErrorAction SilentlyContinue
$probe = Join-Path $Copy 'Assets/SectionLightProbe/Editor'
New-Item -ItemType Directory -Path $probe -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SectionLightProbe.cs') -Destination $probe -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ChessFight.SectionLightProbe.Editor.asmdef') -Destination $probe -Force
$logs = Join-Path $Copy 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Remove-Item -LiteralPath (Join-Path $logs 'SectionLightProbe') -Recurse -ErrorAction SilentlyContinue
$unityLog = Join-Path $logs 'section_light_probe_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.SectionLightProbe.RunPlayBatch',
               '-logFile', "`"$unityLog`"")
if ($Only) { $arguments += @('-probeOnly', $Only) }
Write-Host "Running the section light probe in Unity (batch mode)..."
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Get-Content -LiteralPath (Join-Path $logs 'SectionLightProbe/report.txt') -ErrorAction SilentlyContinue
Write-Host "Unity exit code $($unity.ExitCode); results in $(Join-Path $logs 'SectionLightProbe')"
exit $unity.ExitCode
