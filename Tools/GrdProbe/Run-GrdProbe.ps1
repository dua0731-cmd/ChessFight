param(
    [ValidateSet('baseline', 'ab', 'sweep')][string]$Mode = 'ab',
    [string]$Copy = 'C:\유니티\ChessFight_TestCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [int]$Rounds = 3,
    [int]$Frames = 30,
    [string]$Only = ''
)
# R719: the GPU Resident Drawer probe (Tools/GrdProbe/GrdProbe.cs) on a copy of this project, so the Editor can keep the
# original open (Docs/Environment/GPU_RESIDENT_DRAWER.md). Brings the copy up to date (robocopy: the first time with the
# Library, later never the copy's own Library), takes out the process and lock files a copied Library carries (else the
# copy's Unity kills the open Editor's IL post-processor - PITFALLS 53), puts the probe into <copy>/Assets/GrdProbe/Editor
# with its own editor assembly, runs it in batch mode with a graphics device.
#   baseline  the project's settings as they are (GRD must be off): the Royal Library's three views, timed
#   ab        GRD off / on / on + GPU occlusion culling in turns at the same views: times, pictures, pop-in runs
#   sweep     every course, mini-game room and plain scene: a picture per variant, differences, console messages
#             (-Only courses,minigames,... for a part)
# Results: <copy>/Logs/GrdProbe/<mode> (report.txt, timings.csv, counts.csv, shots.csv, popin.csv, console.txt,
# shots/*.png). Times are one camera drawn into 1920x1080 in the Editor's batch mode - not a player's frame rate.
# The copy's own settings only change for the run; the project in this folder is never written.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
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
foreach ($f in 'ilpp.pid', 'EditorInstance.json', 'ArtifactDB-lock', 'SourceAssetDB-lock') {
    Remove-Item -LiteralPath (Join-Path $Copy "Library/$f") -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath (Join-Path $Copy 'Temp/UnityLockfile') -ErrorAction SilentlyContinue
$probe = Join-Path $Copy 'Assets/GrdProbe/Editor'
New-Item -ItemType Directory -Path $probe -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GrdProbe.cs') -Destination $probe -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ChessFight.GrdProbe.Editor.asmdef') -Destination $probe -Force
$logs = Join-Path $Copy 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Remove-Item -LiteralPath (Join-Path $logs "GrdProbe/$Mode") -Recurse -ErrorAction SilentlyContinue
$unityLog = Join-Path $logs "grd_${Mode}_unity.log"
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.GrdProbe.RunPlayBatch',
               '-grdProbe', $Mode, '-grdProbeRounds', $Rounds, '-grdProbeFrames', $Frames, '-logFile', "`"$unityLog`"")
if ($Only) { $arguments += @('-grdProbeOnly', $Only) }
Write-Host "Running the GRD probe ($Mode) in Unity (batch mode)..."
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Get-Content -LiteralPath (Join-Path $logs "GrdProbe/$Mode/report.txt") -ErrorAction SilentlyContinue
Write-Host "Unity exit code $($unity.ExitCode); results in $(Join-Path $logs "GrdProbe/$Mode")"
exit $unity.ExitCode
