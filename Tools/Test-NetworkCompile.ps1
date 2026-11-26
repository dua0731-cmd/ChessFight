param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [Parameter(Mandatory=$true)][string]$SteamRuntimeSources
)
# Assembly compilation against real Unity/Steamworks sources. Not an Editor import,
# player build, visual test, or two-account Steam connectivity test.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
# asmref-backed prototype code still belongs to the original assembly.
function Get-AssemblySources([string]$AssemblyFolder) {
    foreach ($sourceDirectory in @("$projectRoot/Assets/Scripts/$AssemblyFolder", "$projectRoot/Assets/Prototype/PawnRush/Scripts/$AssemblyFolder")) {
        if (Test-Path -LiteralPath $sourceDirectory) {
            Get-ChildItem -LiteralPath $sourceDirectory -Recurse -Filter '*.cs' | ForEach-Object FullName
        }
    }
}
$output = Join-Path $projectRoot 'Temp/NetworkCompile'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$data = Join-Path $UnityEditor 'Data'
# Use the Editor's current Roslyn toolchain. Its legacy Mono csc predates C# 9 and
# rejects syntax that Unity 6000 itself supports in the existing authoring tools.
$compilerHost = Join-Path $data 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $data 'DotNetSdkRoslyn/csc.dll'
if (!(Test-Path -LiteralPath $compilerHost) -or !(Test-Path -LiteralPath $compiler)) {
    throw "Unity's bundled Roslyn compiler was not found under $data."
}
$baseArgs = @('-nologo','-noconfig','-nostdlib+','-target:library','-langversion:latest')
# Each compile's arguments go in a response file: RagdollLab's sources, one by one, outgrew Windows' 32,767-character
# command line (2026-11-21, "파일 이름이나 확장명이 너무 깁니다").
function Invoke-Csc([string[]]$arguments) {
    $rsp = Join-Path $output ('csc-' + [guid]::NewGuid().ToString('N') + '.rsp')
    [IO.File]::WriteAllLines($rsp, [string[]]($arguments | ForEach-Object { '"' + $_ + '"' }))
    & $compilerHost $compiler "@$rsp"
    Remove-Item -LiteralPath $rsp -ErrorAction SilentlyContinue
}
$standardRefs = Get-ChildItem "$data/NetStandard/ref/2.1.0" -Filter '*.dll' | ForEach-Object { '-r:'+$_.FullName }
$unityRefs = Get-ChildItem "$data/Managed/UnityEngine" -Filter 'UnityEngine*.dll' | ForEach-Object { '-r:'+$_.FullName }
$symbols = '-define:UNITY_EDITOR,UNITY_EDITOR_WIN,UNITY_STANDALONE_WIN,UNITY_STANDALONE,UNITY_2017_1_OR_NEWER'
$steamSources = Get-ChildItem $SteamRuntimeSources -Recurse -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-out:$output/Steamworks.NET.dll") + @($steamSources)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Steamworks source compilation failed.' }
$core = Get-AssemblySources 'Core'
$compileArgs = $baseArgs + @($standardRefs) + @("-out:$output/ChessFight.Network.Core.dll") + @($core)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
$runtime = Get-AssemblySources 'Network'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$output/Steamworks.NET.dll", "-r:$output/ChessFight.Network.Core.dll", "-out:$output/ChessFight.Network.Steam.dll") + @($runtime)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Steam adapter compilation failed.' }
$game = Get-AssemblySources 'Game'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-r:$output/ChessFight.Network.Core.dll", "-out:$output/ChessFight.Game.dll") + @($game)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Game (view/input/HUD) compilation failed.' }
# The VFX workbench and cues. VisualEffect is the engine's VFX module, not the VFX Graph package.
$vfx = Get-AssemblySources 'VFX'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Network.Core.dll", "-out:$output/ChessFight.VFX.dll") + @($vfx)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'VFX workbench compilation failed.' }
# The VFX builders (editor only): generated textures, meshes, materials and prefabs.
$vfxEditor = Get-AssemblySources 'VFXEditor'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-r:$output/ChessFight.VFX.dll", "-out:$output/ChessFight.VFX.Editor.dll") + @($vfxEditor)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'VFX builder compilation failed.' }
$gameplay = Get-AssemblySources 'Gameplay'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Network.Core.dll", "-out:$output/ChessFight.Gameplay.dll") + @($gameplay)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Gameplay (characters/obstacles/course/playtest) compilation failed.' }
$ragdoll = Get-AssemblySources 'RagdollLab'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Gameplay.dll", "-r:$output/ChessFight.Network.Core.dll", "-r:$output/ChessFight.VFX.dll", "-out:$output/ChessFight.RagdollLab.dll") + @($ragdoll)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Ragdoll lab compilation failed.' }
# Pawn Rush course 01 (its Steam bridge, SteamPawnRushLink, sits in RagdollLabSteam and needs it).
$pawnRush = Get-AssemblySources 'PawnRush'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Gameplay.dll", "-r:$output/ChessFight.Network.Core.dll", "-r:$output/ChessFight.VFX.dll", "-out:$output/ChessFight.PawnRush.dll") + @($pawnRush)
# Pawn Rush's sources outgrew Windows' 32767-character command line (R685): its arguments go through a response file.
$pawnRushResponse = Join-Path $output 'PawnRush.rsp'
[System.IO.File]::WriteAllLines($pawnRushResponse, [string[]]($compileArgs | ForEach-Object { '"' + $_ + '"' }), (New-Object System.Text.UTF8Encoding $false))
& $compilerHost $compiler "@$pawnRushResponse"
if ($LASTEXITCODE -ne 0) { throw 'Pawn Rush course compilation failed.' }
# Compile the section authoring tools with the references declared by their own asmdef.
$pawnRushEditor = Get-AssemblySources 'PawnRushEditor'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-r:$output/ChessFight.PawnRush.dll", "-r:$output/ChessFight.Gameplay.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Network.Core.dll", "-out:$output/ChessFight.PawnRush.Editor.dll") + @($pawnRushEditor)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Pawn Rush authoring tools compilation failed.' }
# The lab's Steam bridge and its scene builder, with exactly the references their asmdefs list.
$bootstrap = Get-AssemblySources 'Bootstrap'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$output/Steamworks.NET.dll", "-r:$output/ChessFight.Network.Core.dll", "-r:$output/ChessFight.Network.Steam.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Gameplay.dll", "-out:$output/ChessFight.Game.Steam.dll") + @($bootstrap)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap compilation failed.' }
$ragdollNet = Get-AssemblySources 'RagdollLabSteam'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$output/Steamworks.NET.dll", "-r:$output/ChessFight.Network.Core.dll", "-r:$output/ChessFight.Network.Steam.dll", "-r:$output/ChessFight.RagdollLab.dll", "-r:$output/ChessFight.Gameplay.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Game.Steam.dll", "-r:$output/ChessFight.PawnRush.dll", "-out:$output/ChessFight.RagdollLab.Net.dll") + @($ragdollNet)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Ragdoll lab Steam bridge compilation failed.' }
$ragdollEditor = Get-AssemblySources 'RagdollLabEditor'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-r:$output/ChessFight.RagdollLab.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Gameplay.dll", "-r:$output/ChessFight.Network.Core.dll", "-out:$output/ChessFight.RagdollLab.Editor.dll") + @($ragdollEditor)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Ragdoll lab builder compilation failed.' }
$editor = Get-AssemblySources 'Editor'
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-out:$output/ChessFight.Network.Editor.dll") + @($editor)
Invoke-Csc $compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Editor utility compilation failed.' }
Write-Output 'PASS: Steamworks, Core, Steam adapter, Game, VFX (+ builders), Gameplay, RagdollLab (+ Steam bridge, builder), Pawn Rush (+ authoring tools), Bootstrap and Editor assemblies compiled.'
Write-Output 'NOTE: Assets/Scripts/Input is skipped; it needs the Input System package and compiles inside Unity.'
