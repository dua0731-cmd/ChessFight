param([switch]$Setup, [switch]$Check)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:NODE_DISABLE_COLORS = '1'

# Prefer an installed Node runtime, then the runtime bundled with Codex on this PC.
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
$nodePath = if ($nodeCommand) { $nodeCommand.Source } else { Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe' }
if (-not (Test-Path -LiteralPath $nodePath)) { throw 'Node.js 22 이상을 설치한 뒤 다시 실행하세요: https://nodejs.org/' }
$nodeMajor = [int]((& $nodePath --version).TrimStart('v').Split('.')[0])
if ($nodeMajor -lt 22) { throw 'Node.js 22 이상이 필요합니다.' }

$credentialFile = Join-Path $PSScriptRoot '.local/credentials.xml'
if ($Setup) {
    New-Item -ItemType Directory -Path (Split-Path $credentialFile -Parent) -Force | Out-Null
    Write-Host '키는 이 PC의 현재 Windows 사용자만 열 수 있는 형태로 저장됩니다.'
    Write-Host '키 발급: https://platform.openai.com/api-keys / https://platform.claude.com/settings/keys'
    $openaiSecret = Read-Host 'OpenAI API 키' -AsSecureString
    $anthropicSecret = Read-Host 'Anthropic API 키' -AsSecureString
    if ($openaiSecret.Length -eq 0 -or $anthropicSecret.Length -eq 0) { throw '두 서비스의 API 키를 모두 입력하세요. 기존 설정은 바꾸지 않았습니다.' }
    # Export-Clixml uses Windows DPAPI for SecureString values; plaintext keys are never written.
    [pscustomobject]@{ OpenAI = $openaiSecret; Anthropic = $anthropicSecret } | Export-Clixml -LiteralPath $credentialFile
    Write-Host 'API 키 설정을 저장했습니다.'
}

$oldOpenAI = $env:OPENAI_API_KEY
$oldAnthropic = $env:ANTHROPIC_API_KEY
try {
    foreach ($keyName in @('OPENAI_API_KEY', 'ANTHROPIC_API_KEY')) {
        if (-not [Environment]::GetEnvironmentVariable($keyName, 'Process')) {
            $savedValue = [Environment]::GetEnvironmentVariable($keyName, 'User')
            if ($savedValue) { [Environment]::SetEnvironmentVariable($keyName, $savedValue, 'Process') }
        }
    }
    if (Test-Path -LiteralPath $credentialFile) {
        $savedKeys = Import-Clixml -LiteralPath $credentialFile
        if ($Setup -or -not $env:OPENAI_API_KEY) { $env:OPENAI_API_KEY = [pscredential]::new('api', $savedKeys.OpenAI).GetNetworkCredential().Password }
        if ($Setup -or -not $env:ANTHROPIC_API_KEY) { $env:ANTHROPIC_API_KEY = [pscredential]::new('api', $savedKeys.Anthropic).GetNetworkCredential().Password }
    }
    if ($Check) {
        & $nodePath (Join-Path $PSScriptRoot 'bridge.mjs') --check
    } elseif (-not $env:OPENAI_API_KEY -or -not $env:ANTHROPIC_API_KEY) {
        Write-Host '아직 두 서비스의 API 키가 준비되지 않았습니다. Setup.cmd를 먼저 실행하세요.'
        exit 1
    } else {
        & $nodePath (Join-Path $PSScriptRoot 'bridge.mjs')
    }
    $bridgeExitCode = $LASTEXITCODE
} finally {
    # Do not leave decrypted credentials in the caller's environment.
    $env:OPENAI_API_KEY = $oldOpenAI
    $env:ANTHROPIC_API_KEY = $oldAnthropic
}
exit $bridgeExitCode
