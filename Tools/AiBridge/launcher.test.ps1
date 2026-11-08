$ErrorActionPreference = 'Stop'
$testRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$testDirectory = Join-Path $testRoot ('chessfight-launcher-' + [guid]::NewGuid().ToString('N'))
$oldOpenAI = $env:OPENAI_API_KEY
$oldAnthropic = $env:ANTHROPIC_API_KEY
try {
    New-Item -ItemType Directory -Path $testDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-Api.ps1') -Destination $testDirectory
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bridge.mjs') -Destination $testDirectory
    # Only fake credentials enter the isolated copy; no API request is made.
    function Read-Host {
        param([string]$Prompt, [switch]$AsSecureString)
        if (-not $AsSecureString) { throw 'A credential prompt must use SecureString.' }
        ConvertTo-SecureString 'launcher-test-fake-key' -AsPlainText -Force
    }
    $env:OPENAI_API_KEY = 'existing-test-openai'
    $env:ANTHROPIC_API_KEY = 'existing-test-anthropic'
    & (Join-Path $testDirectory 'Start-Api.ps1') -Setup -Check
    if ($LASTEXITCODE -ne 0) { throw 'Launcher check failed.' }
    $credentialFile = Join-Path $testDirectory '.local/credentials.xml'
    $ciphertext = [System.IO.File]::ReadAllText($credentialFile)
    if ($ciphertext.Contains('launcher-test-fake-key')) { throw 'Plaintext credential was persisted.' }
    $saved = Import-Clixml -LiteralPath $credentialFile
    if ($saved.OpenAI -isnot [Security.SecureString] -or $saved.Anthropic -isnot [Security.SecureString]) { throw 'Credentials were not encrypted SecureString values.' }
    $decoded = [pscredential]::new('test', $saved.OpenAI).GetNetworkCredential().Password
    if ($decoded -ne 'launcher-test-fake-key') { throw 'Credential round trip failed.' }
    if ($env:OPENAI_API_KEY -ne 'existing-test-openai' -or $env:ANTHROPIC_API_KEY -ne 'existing-test-anthropic') { throw 'Launcher changed the caller environment.' }
    $env:OPENAI_API_KEY = $null
    $env:ANTHROPIC_API_KEY = $null
    & (Join-Path $testDirectory 'Start-Api.ps1') -Check
    if ($LASTEXITCODE -ne 0) { throw 'Launcher failed to load the encrypted credentials.' }
    Write-Output 'PASS Windows launcher: masked input, DPAPI save/load, environment restoration, no API request.'
} finally {
    $env:OPENAI_API_KEY = $oldOpenAI
    $env:ANTHROPIC_API_KEY = $oldAnthropic
    $absoluteTestPath = [System.IO.Path]::GetFullPath($testDirectory)
    if (-not $absoluteTestPath.StartsWith($testRoot, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $absoluteTestPath -Leaf) -notlike 'chessfight-launcher-*') { throw 'Unexpected cleanup path.' }
    if (Test-Path -LiteralPath $absoluteTestPath) { Remove-Item -LiteralPath $absoluteTestPath -Recurse -Force }
}
