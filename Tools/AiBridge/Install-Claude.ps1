$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (-not [Environment]::Is64BitProcess) { throw '64비트 PowerShell이 필요합니다.' }

# Use the same release source and SHA256 manifest as Anthropic's official Windows installer.
$releaseBase = 'https://downloads.claude.ai/claude-code-releases'
$releaseVersion = ([string](Invoke-RestMethod -Uri "$releaseBase/latest")).Trim()
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw '공식 배포 버전을 확인할 수 없습니다.' }
$platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win32-arm64' } else { 'win32-x64' }
$manifest = Invoke-RestMethod -Uri "$releaseBase/$releaseVersion/manifest.json"
$expectedHash = [string]$manifest.platforms.$platform.checksum
if ($expectedHash -notmatch '^[a-fA-F0-9]{64}$') { throw '공식 SHA256 값을 확인할 수 없습니다.' }
$installDirectory = Join-Path $PSScriptRoot '.local/bin'
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
$binaryPath = Join-Path $installDirectory 'claude.exe'
$downloadPath = Join-Path $installDirectory 'claude.exe.download'
$alreadyInstalled = (Test-Path -LiteralPath $binaryPath) -and ((Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash -eq $expectedHash)
if (-not $alreadyInstalled) {
    Write-Host 'Claude Code를 공식 배포처에서 내려받고 있습니다.'
    Invoke-WebRequest -Uri "$releaseBase/$releaseVersion/$platform/claude.exe" -OutFile $downloadPath
    if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash -ne $expectedHash) {
        Remove-Item -LiteralPath $downloadPath -Force
        throw '다운로드한 파일의 SHA256 값이 일치하지 않습니다.'
    }
    Move-Item -LiteralPath $downloadPath -Destination $binaryPath -Force
}
[pscustomobject]@{ version = $releaseVersion; platform = $platform; sha256 = $expectedHash } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot '.local/claude-release.json') -Encoding UTF8
Write-Host "Claude Code $releaseVersion 준비 완료 (이 도구 폴더에만 설치)."
