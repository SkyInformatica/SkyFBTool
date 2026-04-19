param(
  [string]$HostName = "localhost",
  [int]$Port = 3050,
  [string]$User = "sysdba",
  [string]$Password = "masterkey"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$env:SKYFBTOOL_TEST_RUN_INTEGRATION = "true"
$env:SKYFBTOOL_TEST_DB_HOST = $HostName
$env:SKYFBTOOL_TEST_DB_PORT = "$Port"
$env:SKYFBTOOL_TEST_DB_USER = $User
$env:SKYFBTOOL_TEST_DB_PASSWORD = $Password

Write-Host "Executando testes de integracao..."
Write-Host "Host: $HostName | Porta: $Port | Usuario: $User"

dotnet test .\SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
exit $LASTEXITCODE
