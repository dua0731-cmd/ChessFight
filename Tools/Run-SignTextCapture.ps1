# R753: pictures of the course's translated 3D words (SignTextCapture.RunPlayBatch) on the project copy, so the open
# Editor is left alone: the royal carriage yard and a mini-game room, each sign from in front, in one language.
#   ./Tools/Run-SignTextCapture.ps1 -Language zh-Hans      (en, ko, any column of the string table)
# Results: <copy>/Logs/SignText/<language> (report.txt, pictures).
param([string]$Language = 'zh-Hans')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$Copy = 'C:\유니티\ChessFight_TestCopy'
$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor'
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like '*ChessFight_TestCopy*' }
if ($busy) { throw "Another Unity is running on the copy (pid $($busy.ProcessId -join ', ')); try again when it is done." }
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
Remove-Item -LiteralPath (Join-Path $logs "SignText/$Language") -Recurse -ErrorAction SilentlyContinue
$unityLog = Join-Path $logs "sign_text_$Language.log"
$arguments = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', 'ChessFight.PawnRush.Editor.SignTextCapture.RunPlayBatch', '-captureLanguage', $Language, '-logFile', "`"$unityLog`"")
Write-Host "Taking the sign pictures in Unity (batch mode, $Language)..."
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Write-Host "Unity exit code $($unity.ExitCode)"
Get-Content -LiteralPath (Join-Path $logs "SignText/$Language/report.txt") -Encoding UTF8 -ErrorAction SilentlyContinue
exit $unity.ExitCode
