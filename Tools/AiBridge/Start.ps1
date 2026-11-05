param([switch]$Setup, [switch]$Check)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$nodeCommand = Get-Command node -CommandType Application -ErrorAction SilentlyContinue
$nodePath = if ($nodeCommand) { $nodeCommand.Source } else { Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe' }
if (-not (Test-Path -LiteralPath $nodePath)) { throw 'Node.js 22 이상을 설치한 뒤 다시 실행하세요: https://nodejs.org/' }
if ([int]((& $nodePath --version).TrimStart('v').Split('.')[0]) -lt 22) { throw 'Node.js 22 이상이 필요합니다.' }
$bridgePath = Join-Path $PSScriptRoot 'cli-bridge.mjs'
$moduleUri = ([uri]$bridgePath).AbsoluteUri
function Get-BridgePaths {
    $pathsJson = & $nodePath --input-type=module -e 'const m=await import(process.argv[1]); console.log(JSON.stringify((await m.cliConfiguration()).paths));' $moduleUri
    if ($LASTEXITCODE -ne 0) { throw '프로그램 경로를 확인하지 못했습니다.' }
    $pathsJson | ConvertFrom-Json
}
function Get-BridgeAccounts {
    $stateJson = & $nodePath $bridgePath --check --json
    if (-not $stateJson) { throw '계정 로그인 상태를 확인하지 못했습니다.' }
    $stateJson | ConvertFrom-Json
}
$paths = Get-BridgePaths
if (-not $paths.claude -and -not $Check) {
    & (Join-Path $PSScriptRoot 'Install-Claude.ps1')
    $paths = Get-BridgePaths
}
if (-not $paths.gpt -and -not $Check) { throw 'Codex를 찾지 못했습니다. Codex 앱을 설치하고 다시 실행하세요.' }

# Login and model calls use native account credentials, with no API-key fallback.
$removedNames = @('OPENAI_API_KEY', 'CODEX_API_KEY', 'CODEX_ACCESS_TOKEN', 'OPENAI_BASE_URL', 'OPENAI_API_BASE',
    'ANTHROPIC_API_KEY', 'ANTHROPIC_AUTH_TOKEN', 'ANTHROPIC_BASE_URL', 'ANTHROPIC_PROFILE',
    'ANTHROPIC_FEDERATION_RULE_ID', 'ANTHROPIC_ORGANIZATION_ID', 'CLAUDE_CODE_OAUTH_TOKEN',
    'CLAUDE_CODE_USE_BEDROCK', 'CLAUDE_CODE_USE_VERTEX', 'CLAUDE_CODE_USE_FOUNDRY')
$oldEnvironment = @{}
foreach ($entryName in $removedNames) {
    $oldEnvironment[$entryName] = [Environment]::GetEnvironmentVariable($entryName, 'Process')
    [Environment]::SetEnvironmentVariable($entryName, $null, 'Process')
}
try {
    $state = Get-BridgeAccounts
    if ($Setup) {
        if (-not $state.gpt.ready) {
            Write-Host 'ChatGPT 계정으로 Codex에 로그인하세요.'
            & $paths.gpt login
            if ($LASTEXITCODE -ne 0) { throw 'Codex 로그인을 마치지 못했습니다.' }
        }
        if (-not $state.claude.ready) {
            Write-Host 'Claude 계정으로 로그인하세요. 코드가 표시되면 이 창에 직접 붙여 넣으세요.'
            & $paths.claude auth login
            if ($LASTEXITCODE -ne 0) { throw 'Claude 로그인을 마치지 못했습니다.' }
        }
        $state = Get-BridgeAccounts
    }
    if ($Check) {
        Write-Host "GPT(Codex): $($state.gpt.reason)"
        Write-Host "Claude Code: $($state.claude.reason)"
        $bridgeExitCode = if ($state.gpt.ready -and $state.claude.ready) { 0 } else { 1 }
    } elseif (-not $state.gpt.ready -or -not $state.claude.ready) {
        Write-Host 'Setup.cmd를 먼저 실행해 계정 로그인을 완료하세요. API 키는 필요하지 않습니다.'
        $bridgeExitCode = 1
    } else {
        & $nodePath $bridgePath
        $bridgeExitCode = $LASTEXITCODE
    }
} finally {
    foreach ($entryName in $removedNames) { [Environment]::SetEnvironmentVariable($entryName, $oldEnvironment[$entryName], 'Process') }
}
exit $bridgeExitCode
