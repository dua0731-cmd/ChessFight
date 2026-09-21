param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$mono = Join-Path $UnityEditor 'Data/MonoBleedingEdge/bin/mono.exe'
$compiler = Join-Path $UnityEditor 'Data/MonoBleedingEdge/lib/mono/4.5/csc.exe'
$outputDirectory = Join-Path $projectRoot 'Temp/NetworkTests'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$output = Join-Path $outputDirectory 'NetworkCoreTests.exe'
$sources = Get-ChildItem (Join-Path $projectRoot 'Assets/ChessFight/Network/Core') -Filter '*.cs' | ForEach-Object FullName
$sources += Join-Path $projectRoot 'Tests/Network/NetworkCoreTests.cs'
& $mono $compiler '-nologo' '-langversion:latest' "-out:$output" @sources
if ($LASTEXITCODE -ne 0) { throw 'Core test compilation failed.' }
& $mono $output
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
$sessionOutput = Join-Path $outputDirectory 'SessionFlowTests.exe'
$sessionSources = Get-ChildItem (Join-Path $projectRoot 'Assets/ChessFight/Network/Core') -Filter '*.cs' | ForEach-Object FullName
$sessionSources += Join-Path $projectRoot 'Assets/ChessFight/Network/Steam/SteamSession.cs'
$sessionSources += Join-Path $projectRoot 'Tests/Network/FakeSteam.cs'
$sessionSources += Join-Path $projectRoot 'Tests/Network/SessionFlowTests.cs'
& $mono $compiler '-nologo' '-langversion:latest' '-r:System.Web.Extensions.dll' "-out:$sessionOutput" @sessionSources
if ($LASTEXITCODE -ne 0) { throw 'Session test compilation failed.' }
& $mono $sessionOutput
if ($LASTEXITCODE -ne 0) { throw 'Session tests failed.' }
