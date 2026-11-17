param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'backend/UbbEvents.Api/UbbEvents.Api.csproj'
$credentialsPath = Join-Path $repositoryRoot '.env.demo-seed.local'
$previousPassword = [Environment]::GetEnvironmentVariable('DemoSeed__Password', 'Process')

try {
    if (-not $DryRun -and [string]::IsNullOrEmpty($previousPassword)) {
        if (Test-Path -LiteralPath $credentialsPath) {
            $passwordLine = @(Get-Content -LiteralPath $credentialsPath | Where-Object { $_ -match '^DemoSeed__Password=' })
            if ($passwordLine.Count -ne 1) { throw 'Expected exactly one DemoSeed__Password entry in .env.demo-seed.local.' }
            $env:DemoSeed__Password = $passwordLine[0].Substring('DemoSeed__Password='.Length)
        } else {
            $passwordBytes = New-Object byte[] 24
            $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
            try { $generator.GetBytes($passwordBytes) } finally { $generator.Dispose() }
            $env:DemoSeed__Password = [Convert]::ToBase64String($passwordBytes)
            Set-Content -LiteralPath $credentialsPath -Value "DemoSeed__Password=$($env:DemoSeed__Password)" -Encoding UTF8
            Write-Output 'Created .env.demo-seed.local with a generated demo-account password. Keep this ignored file private.'
        }
    }
    # Use the Release output so a running Debug API does not lock the executable being built.
    $seedArguments = @('run', '--project', $projectPath, '--configuration', 'Release', '--launch-profile', 'http', '--', '--seed-demo')
    if ($DryRun) { $seedArguments += '--dry-run' }
    & dotnet @seedArguments
    if ($LASTEXITCODE -ne 0) { throw "Demo seeding exited with code $LASTEXITCODE. See the message above." }
} finally {
    [Environment]::SetEnvironmentVariable('DemoSeed__Password', $previousPassword, 'Process')
}
