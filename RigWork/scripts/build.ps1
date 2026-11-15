param(
    [Parameter(Mandatory = $true)][string]$Method,   # e.g. ChessFight.RagdollLab.Editor.RagdollLabBuilder.BuildPlayerBatch
    [Parameter(Mandatory = $true)][string]$Out,      # player folder name under proj
    [string]$Project = 'proj',
    [string[]]$Extra = @()
)
$ErrorActionPreference = 'Stop'
$scratch = Split-Path $PSCommandPath
$dst = Join-Path $scratch $Project
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe'
$log = Join-Path $dst ("build_" + $Out + ".log")
$a = @('-batchmode', '-nographics', '-projectPath', "`"$dst`"", '-executeMethod', $Method, '-labOut', "`"$dst\$Out`"", '-logFile', "`"$log`"") + $Extra
$p = Start-Process -FilePath $unity -ArgumentList $a -PassThru -Wait
$errs = Select-String -Path $log -Pattern 'error CS|Exception|Shader error' | Select-Object -First 8 | ForEach-Object { $_.Line }
if ($p.ExitCode -ne 0 -or $errs) { "BUILD FAILED ($($p.ExitCode))"; $errs; exit 1 }
"build ok: $dst\$Out"
