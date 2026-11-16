param(
    [string]$Copy = 'C:\Temp\CF_ColliderCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [string]$Python = 'C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe'
)
# R684: the Royal Carriage Yard's collider audit (ChessFight.PawnRush.Editor.CarriageYardColliderAudit) on a copy of this
# project, so the Editor can keep the original open. Brings the copy up to date (robocopy: the first time with the
# Library, later never the copy's own Library), runs the audit in batch mode, then Tools/ColliderAudit/reach.py on each
# scan (Blender's bundled Python: `python` on these PCs is a Store stub). Results: <copy>/Logs/YardColliderAudit
# (*_surfaces.csv, *_side.csv, *_names.csv, and <scan>.txt, the summary by object).
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
# The copied Library names the open Editor's processes: the copy's Unity would kill the original's IL post-processor
# (seen 11-16: "Found a lingering IL Post Processing runner process ... Killing it") and take its lock files for its own.
foreach ($f in 'ilpp.pid', 'EditorInstance.json', 'ArtifactDB-lock', 'SourceAssetDB-lock') {
    Remove-Item -LiteralPath (Join-Path $Copy "Library/$f") -ErrorAction SilentlyContinue
}
$logs = Join-Path $Copy 'Logs'
$out = Join-Path $logs 'YardColliderAudit'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Remove-Item -LiteralPath $out -Recurse -ErrorAction SilentlyContinue
$unityLog = Join-Path $logs 'collider_audit_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.CarriageYardColliderAudit.RunBatch',
               '-logFile', "`"$unityLog`"")
Write-Host "Running the collider audit in Unity (batch mode)..."
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Get-Content -LiteralPath (Join-Path $out 'log.txt') -ErrorAction SilentlyContinue
foreach ($scan in Get-ChildItem -LiteralPath $out -Filter '*_names.csv') {
    $label = $scan.Name -replace '_names\.csv$', ''
    & $Python -I (Join-Path $PSScriptRoot 'reach.py') $out $label (Join-Path $out "$label.txt")
}
Write-Host "Unity exit code $($unity.ExitCode); results in $out"
exit $unity.ExitCode
