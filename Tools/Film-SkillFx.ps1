param(
    [string]$Shots = 'set3',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor'
)
# Films the skills' effect contact sheets (PawnRushFxSheet: one JPG a skill, every few frames of game time, four across)
# in a batch-mode Unity on this project, with a graphics device: Logs/FxSheets/<shot>.jpg, Unity's log beside them.
# -Shots: comma-separated shot names (PawnRushFxSheet.Shots) or "set3". Refuses while a Unity editor is open (it would
# hold the project), so close Unity first.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$open = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue
if ($open) { throw "A Unity editor is running (pid $($open.ProcessId -join ', ')); close it first." }
$out = Join-Path $root 'Logs/FxSheets'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$log = Join-Path $root 'Logs/fxsheets_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$root`"", '-executeMethod', 'ChessFight.RagdollLab.Editor.PawnRushFxSheetBatch.Run',
               '-fxShots', $Shots, '-fxOut', "`"$out`"", '-logFile', "`"$log`"")
$started = Get-Date
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Select-String -Path $log -Pattern '\[FxSheet\]|error CS|Exception' -ErrorAction SilentlyContinue | Select-Object -Last 40 | ForEach-Object { $_.Line }
Get-ChildItem $out -Filter *.jpg | Where-Object { $_.LastWriteTime -ge $started } | ForEach-Object { $_.FullName }
Write-Host "Unity exit code $($unity.ExitCode) after $([int]((Get-Date) - $started).TotalSeconds) s"
exit $unity.ExitCode
