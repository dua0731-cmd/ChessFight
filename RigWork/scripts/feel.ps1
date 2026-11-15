param(
    [Parameter(Mandatory = $true)][string]$Player,  # build folder under proj
    [Parameter(Mandatory = $true)][string]$Out,     # output folder under the scratchpad
    [string]$Only = ''
)
$ErrorActionPreference = 'Stop'
$scratch = Split-Path $PSCommandPath
$outDir = Join-Path $scratch $Out
New-Item -ItemType Directory -Force $outDir | Out-Null
$exe = Join-Path $scratch "proj\$Player\RagdollLab.exe"
$a = @('-batchmode', '-ragdollFeel', "`"$outDir`"", '-screen-width', '800', '-screen-height', '450', '-logFile', "`"$outDir\feel.log`"")
if ($Only) { $a += @('-ragdollFeelOnly', $Only) }
$r = Start-Process -FilePath $exe -ArgumentList $a -PassThru
if (-not $r.WaitForExit(1200000)) { 'feel still running'; exit 1 }
"feel exit $($r.ExitCode)"
Get-ChildItem $outDir -Directory | ForEach-Object { "{0}: {1} frames" -f $_.Name, (Get-ChildItem $_.FullName -Filter 'f_*.png').Count }
