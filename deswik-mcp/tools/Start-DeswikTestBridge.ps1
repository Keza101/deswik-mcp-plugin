[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $RepoRoot,
    [Parameter(Mandatory)] [string] $PythonPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$host.UI.RawUI.WindowTitle = 'Deswik MCP bridge - live tests'
Set-Location -LiteralPath $RepoRoot
$env:DESWIK_MCP_PYTHON = $PythonPath
$bridge = Join-Path $RepoRoot 'deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe'

Write-Host 'DESWIK MCP TEST BRIDGE' -ForegroundColor Cyan
Write-Host 'Leave this window open during the live checks.' -ForegroundColor Yellow
Write-Host 'Press Ctrl+C here when testing is finished.'
Write-Host ''
& $bridge
$bridgeExit = $LASTEXITCODE
Write-Host "`nBridge stopped with exit code $bridgeExit." -ForegroundColor Yellow
Read-Host 'Press Enter to close this window'
exit $bridgeExit
