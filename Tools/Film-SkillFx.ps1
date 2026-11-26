param(
    [string]$Shots = 'set3',
    [string]$Note = '',
    [string]$Video = '',
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor'
)
# Films the skills' effects (PawnRushFxSheet) in a batch-mode Unity on this project, with a graphics device: every frame
# of each shot (30 a second of game time, 960 x 540, captioned) in Logs/FxSheets/<shot>/, a contact sheet
# Logs/FxSheets/<shot>.jpg (24 frames, four across), and one video of them all, each shot at full speed and then at half
# speed (Logs/FxSheets/<Video or "skills">.mp4, Krita's ffmpeg). Unity's log: Logs/fxsheets_unity.log.
# -Shots: comma-separated shot names (PawnRushFxSheet.Shots), "set3", "set4a" or "set4b" (the 4판's bundles 가 and 나). -Note: the
# caption's second line.
# Refuses while a Unity editor is open (it would hold the project), so close Unity first.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# Only a Unity that has this project open is in the way (other projects' batch runs are not).
$mine = $root.Replace('\', '/').ToLowerInvariant()
$open = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\', '/').ToLowerInvariant().Contains($mine) }
if ($open) { throw "A Unity editor has this project open (pid $($open.ProcessId -join ', ')); close it first." }
$out = Join-Path $root 'Logs/FxSheets'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$log = Join-Path $root 'Logs/fxsheets_unity.log'
$arguments = @('-batchmode', '-projectPath', "`"$root`"", '-executeMethod', 'ChessFight.RagdollLab.Editor.PawnRushFxSheetBatch.Run',
               '-fxShots', $Shots, '-fxOut', "`"$out`"", '-logFile', "`"$log`"")
if ($Note) { $arguments += @('-fxNote', "`"$Note`"") }
$started = Get-Date
$unity = Start-Process -FilePath (Join-Path $UnityEditor 'Unity.exe') -ArgumentList $arguments -Wait -PassThru
Select-String -Path $log -Pattern '\[FxSheet\]|error CS|Exception' -ErrorAction SilentlyContinue | Select-Object -Last 40 | ForEach-Object { $_.Line }
Write-Host "Unity exit code $($unity.ExitCode) after $([int]((Get-Date) - $started).TotalSeconds) s"
if ($unity.ExitCode -ne 0) { exit $unity.ExitCode }

# The video: the shots in the order filmed, each at full speed then at half speed (every frame twice).
$names = @()
Select-String -Path $log -Pattern '\[FxSheet\] (\w+): \d+ frames in' | ForEach-Object { $names += $_.Matches[0].Groups[1].Value }
if ($names.Count -eq 0) { Write-Host 'No frames filmed.'; exit 1 }
$seq = Join-Path $out '_video'
if (Test-Path $seq) { Remove-Item $seq -Recurse -Force }
New-Item -ItemType Directory -Path $seq | Out-Null
$n = 0
foreach ($name in $names) {
    $frames = Get-ChildItem (Join-Path $out $name) -Filter 'f_*.jpg' | Sort-Object Name
    foreach ($pass in 1, 2) {
        foreach ($f in $frames) {
            for ($k = 0; $k -lt $pass; $k++) {
                New-Item -ItemType HardLink -Path (Join-Path $seq ('f_{0:D5}.jpg' -f $n)) -Target $f.FullName | Out-Null
                $n++
            }
        }
    }
}
$mp4 = Join-Path $out ($(if ($Video) { $Video } else { 'skills' }) + '.mp4')
& 'C:\Program Files\Krita (x64)\bin\ffmpeg.exe' -y -loglevel error -framerate 30 -i (Join-Path $seq 'f_%05d.jpg') -c:v libopenh264 -b:v 1800k -profile:v high -pix_fmt yuv420p $mp4
Remove-Item $seq -Recurse -Force
Write-Host "Video: $mp4 ($n frames, $([math]::Round((Get-Item $mp4).Length / 1MB, 1)) MB)"
