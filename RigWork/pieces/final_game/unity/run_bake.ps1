# Bakes the staged bishop, king and queen in the batch copy of the Unity project and renders the check images.
#   1. overlays RigWork/pieces/final_game/stage/Assets onto <copy>/Assets (models, atlases, materials)
#   2. PieceModelBaker.BakeAll, PieceModelBaker.BakePawnRush, MenuPieceBaker.BakeAll (batch, no graphics)
#   3. PieceCheckRender.Run (batch with graphics) -> <copy>/CheckRender/*.png
# Usage: run_bake.ps1 [-Copy C:\Users\trews\cf_batch\ChessFight] [-Skip overlay,bake,render]
param(
    [string]$Copy = 'C:\Users\trews\cf_batch\ChessFight',
    [string[]]$Skip = @()
)
$ErrorActionPreference = 'Stop'
$here = Split-Path $PSCommandPath
$stage = Join-Path (Split-Path $here) 'stage\Assets'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe'

function Invoke-Unity([string]$Method, [string]$Log, [switch]$Graphics) {
    $a = @('-batchmode', '-projectPath', "`"$Copy`"", '-executeMethod', $Method, '-logFile', "`"$Log`"", '-quit')
    if (-not $Graphics) { $a += '-nographics' }
    $t0 = Get-Date
    $p = Start-Process -FilePath $unity -ArgumentList $a -PassThru -Wait
    $errs = Select-String -Path $Log -Pattern 'error CS\d+|Exception:|Shader error|has no model|has no (left|right)' | Select-Object -First 12 | ForEach-Object { $_.Line }
    $out = Select-String -Path $Log -Pattern '^\[PieceModels|^\[MenuPieces|^\[Check\]' | ForEach-Object { $_.Line }
    "{0}: exit {1} in {2:N0} s" -f $Method, $p.ExitCode, ((Get-Date) - $t0).TotalSeconds
    $out
    if ($errs) { 'ERRORS:'; $errs }
}

if ($Skip -notcontains 'overlay') {
    robocopy $stage (Join-Path $Copy 'Assets') /E /NFL /NDL /NJH /NJS /NP | Out-Null
    New-Item -ItemType Directory -Force (Join-Path $Copy 'Assets\Editor') | Out-Null
    Copy-Item (Join-Path $here 'PieceCheckRender.cs') (Join-Path $Copy 'Assets\Editor\PieceCheckRender.cs') -Force
    'overlay done'
}
if ($Skip -notcontains 'bake') {
    Invoke-Unity 'ChessFight.RagdollLab.Editor.PieceModelBaker.BakeAll' (Join-Path $Copy 'bake_pieces.log')
    Invoke-Unity 'ChessFight.RagdollLab.Editor.PieceModelBaker.BakePawnRush' (Join-Path $Copy 'bake_pawnrush.log')
    Invoke-Unity 'ChessFight.RagdollLab.Editor.MenuPieceBaker.BakeAll' (Join-Path $Copy 'bake_menu.log')
}
if ($Skip -notcontains 'render') {
    Invoke-Unity 'PieceCheckRender.Run' (Join-Path $Copy 'check_render.log') -Graphics
    Get-ChildItem (Join-Path $Copy 'CheckRender') -Filter *.png -ErrorAction SilentlyContinue | Measure-Object | ForEach-Object { "check images: $($_.Count)" }
}
