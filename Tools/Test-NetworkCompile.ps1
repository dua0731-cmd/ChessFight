param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor',
    [Parameter(Mandatory=$true)][string]$SteamRuntimeSources
)
# Assembly compilation against real Unity/Steamworks sources. Not an Editor import,
# player build, visual test, or two-account Steam connectivity test.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot 'Temp/NetworkCompile'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$data = Join-Path $UnityEditor 'Data'
$mono = Join-Path $data 'MonoBleedingEdge/bin/mono.exe'
$compiler = Join-Path $data 'MonoBleedingEdge/lib/mono/4.5/csc.exe'
$baseArgs = @('-nologo','-noconfig','-nostdlib+','-target:library','-langversion:latest')
$standardRefs = Get-ChildItem "$data/NetStandard/ref/2.1.0" -Filter '*.dll' | ForEach-Object { '-r:'+$_.FullName }
$unityRefs = Get-ChildItem "$data/Managed/UnityEngine" -Filter 'UnityEngine*.dll' | ForEach-Object { '-r:'+$_.FullName }
$symbols = '-define:UNITY_EDITOR,UNITY_EDITOR_WIN,UNITY_STANDALONE_WIN,UNITY_STANDALONE,UNITY_2017_1_OR_NEWER'
$steamSources = Get-ChildItem $SteamRuntimeSources -Recurse -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-out:$output/Steamworks.NET.dll") + @($steamSources)
& $mono $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Steamworks source compilation failed.' }
$core = Get-ChildItem "$projectRoot/Assets/Scripts/Core" -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @("-out:$output/ChessFight.Network.Core.dll") + @($core)
& $mono $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
$runtime = Get-ChildItem "$projectRoot/Assets/Scripts/Network" -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$output/Steamworks.NET.dll", "-r:$output/ChessFight.Network.Core.dll", "-out:$output/ChessFight.Network.Steam.dll") + @($runtime)
& $mono $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Steam adapter compilation failed.' }
$game = Get-ChildItem "$projectRoot/Assets/Scripts/Game" -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$output/ChessFight.Network.Core.dll", "-out:$output/ChessFight.Game.dll") + @($game)
& $mono $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Game (view/input/HUD) compilation failed.' }
$gameplay = Get-ChildItem "$projectRoot/Assets/Scripts/Gameplay" -Recurse -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$output/ChessFight.Game.dll", "-out:$output/ChessFight.Gameplay.dll") + @($gameplay)
& $mono $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Gameplay (characters/obstacles/course/playtest) compilation failed.' }
$ragdoll = Get-ChildItem "$projectRoot/Assets/ChessFight/RagdollLab/Scripts" -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Gameplay.dll", "-out:$output/ChessFight.RagdollLab.dll") + @($ragdoll)
& $mono $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Ragdoll lab compilation failed.' }
$bootstrap = Get-ChildItem "$projectRoot/Assets/Scripts/Bootstrap" -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$output/Steamworks.NET.dll", "-r:$output/ChessFight.Network.Core.dll", "-r:$output/ChessFight.Network.Steam.dll", "-r:$output/ChessFight.Game.dll", "-r:$output/ChessFight.Gameplay.dll", "-out:$output/ChessFight.Game.Steam.dll") + @($bootstrap)
& $mono $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap compilation failed.' }
$editor = Get-ChildItem "$projectRoot/Assets/Scripts/Editor" -Filter '*.cs' | ForEach-Object FullName
$compileArgs = $baseArgs + @($standardRefs) + @($unityRefs) + @($symbols, "-r:$data/Managed/UnityEditor.dll", "-out:$output/ChessFight.Network.Editor.dll") + @($editor)
& $mono $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Editor utility compilation failed.' }
Write-Output 'PASS: Steamworks, Core, Steam adapter, Game, Gameplay, RagdollLab, Bootstrap and Editor assemblies compiled.'
Write-Output 'NOTE: Assets/Scripts/Input is skipped; it needs the Input System package and compiles inside Unity.'
