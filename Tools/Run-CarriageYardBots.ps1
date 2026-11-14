param(
    [string]$Copy = 'C:\유니티\ChessFight_TestCopy',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [int]$Runs = 4,
    [ValidateSet(0, 1, 2)][int]$Zone = 0,
    [int]$Seed = 7,
    [switch]$NoCapture
)
# R660: the Royal Carriage Yard's bot test on a copy of this project (the user: "사본과 시험 봇"), so the Editor can keep
# the original open. Brings the copy up to date with this folder (robocopy: the first time everything with the Library,
# later only what changed and never the copy's own Library), then runs ChessFight.PawnRush.Editor.CarriageYardBotTest in
# batch mode there with a graphics device (pictures). Twelve ragdoll pawns run zone 1 and zone 2 `Runs` times each.
# Results: <copy>/Logs/CarriageYardBots (report.txt, crossings.csv, hits.csv, *.png); Unity's log beside them.
# Not a play test with people and not online: blind runners and slam jumpers, the real physics.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
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
$logs = Join-Path $Copy 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$unityLog = Join-Path $logs 'yardbots_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.CarriageYardBotTest.RunPlayBatch',
               '-yardBotRuns', $Runs, '-yardBotZone', $Zone, '-yardBotSeed', $Seed, '-logFile', "`"$unityLog`"")
if ($NoCapture) { $arguments += '-yardBotNoCapture' }
Write-Host "Running the bot test in Unity (batch mode)..."
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
$report = Join-Path $logs 'CarriageYardBots/report.txt'
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Tail 30 }
Write-Host "Unity exit code $($unity.ExitCode); results in $(Join-Path $logs 'CarriageYardBots')"
exit $unity.ExitCode
