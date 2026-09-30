[CmdletBinding()]
param(
    [string] $DeswikDir = $env:DESWIK_DIR,
    [switch] $NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Core') {
    throw 'PowerShell 7 is required for the add-in preflight. Install pwsh or place pwsh.exe in PATH.'
}

function Resolve-DeswikDirectory {
    param([string] $RequestedPath)
    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path -LiteralPath (Join-Path $RequestedPath 'Deswik.Graphics.dll') -PathType Leaf)) {
            throw "DESWIK_DIR does not contain Deswik.Graphics.dll: $RequestedPath"
        }
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }
    $preferred = 'C:\Program Files\Deswik\Deswik.Suite 2025.2'
    if (Test-Path -LiteralPath (Join-Path $preferred 'Deswik.Graphics.dll') -PathType Leaf) { return $preferred }
    $candidate = Get-ChildItem -LiteralPath 'C:\Program Files\Deswik' -Directory -Filter 'Deswik.Suite *' -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'Deswik.Graphics.dll') -PathType Leaf } |
        Sort-Object Name -Descending | Select-Object -First 1
    if ($candidate) { return $candidate.FullName }
    throw 'No Deswik.Suite installation was found. Set DESWIK_DIR and run the launcher again.'
}

function Resolve-DotnetExecutable {
    $bundled = Join-Path $env:USERPROFILE '.dotnet-sdk-8\dotnet.exe'
    if (Test-Path -LiteralPath $bundled -PathType Leaf) { return $bundled }
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw 'The .NET SDK was not found in PATH or USERPROFILE\.dotnet-sdk-8.'
}

function Test-TcpPortOpen {
    param([int] $Port)
    $client = [Net.Sockets.TcpClient]::new()
    try {
        $pending = $client.BeginConnect('127.0.0.1', $Port, $null, $null)
        if (-not $pending.AsyncWaitHandle.WaitOne(300)) { return $false }
        $client.EndConnect($pending)
        return $client.Connected
    }
    catch { return $false }
    finally { $client.Dispose() }
}

function Invoke-Build {
    param([string] $Dotnet, [string] $Project, [string] $ResolvedDeswikDir)
    Write-Host "`nBuilding $Project" -ForegroundColor Cyan
    & $Dotnet build $Project -c Release --nologo '-consoleLoggerParameters:ErrorsOnly' "-p:DeswikDir=$ResolvedDeswikDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $Project" }
}

$host.UI.RawUI.WindowTitle = 'Deswik live test setup'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
Set-Location -LiteralPath $repoRoot
if (Get-Process 'Deswik.CAD' -ErrorAction SilentlyContinue) {
    throw 'Close Deswik.CAD before running setup so the add-in can be rebuilt and checked.'
}
if (Test-TcpPortOpen -Port 9595) {
    throw 'Port 9595 is already in use. Stop the old bridge with Ctrl+C, then run this launcher again.'
}

$resolvedDeswikDir = Resolve-DeswikDirectory -RequestedPath $DeswikDir
$dotnetExe = Resolve-DotnetExecutable
$pythonExe = (Get-Command python -ErrorAction Stop).Source
$env:DESWIK_DIR = $resolvedDeswikDir
$env:DESWIK_MCP_PYTHON = $pythonExe

Write-Host 'DESWIK LIVE ACCEPTANCE SETUP' -ForegroundColor Cyan
Write-Host "Repository: $repoRoot"
Write-Host "Deswik:     $resolvedDeswikDir"
Write-Host "dotnet:     $dotnetExe"
Write-Host "Python:     $pythonExe"

Invoke-Build $dotnetExe 'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' $resolvedDeswikDir
Invoke-Build $dotnetExe 'deswik-mcp\src\Deswik.Bridge.Standalone\Deswik.Bridge.Standalone.csproj' $resolvedDeswikDir
Invoke-Build $dotnetExe 'deswik-mcp\src\Deswik.Bridge.Tests\Deswik.Bridge.Tests.csproj' $resolvedDeswikDir

foreach ($pythonTest in @('tests\test_roundtrip.py', 'tests\test_commands.py')) {
    Write-Host "`nRunning $pythonTest" -ForegroundColor Cyan
    & $pythonExe $pythonTest
    if ($LASTEXITCODE -ne 0) { throw "Python regression failed: $pythonTest" }
}

$testHarness = Join-Path $repoRoot 'deswik-mcp\src\Deswik.Bridge.Tests\bin\Release\net8.0\Deswik.Bridge.Tests.exe'
Write-Host "`nRunning deterministic bridge checks" -ForegroundColor Cyan
& $testHarness
if ($LASTEXITCODE -ne 0) { throw 'The deterministic bridge checks failed.' }

Write-Host "`nRunning add-in preflight" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'Test-AddinPreflight.ps1') -DeswikDir $resolvedDeswikDir
if ($NoLaunch) {
    Write-Host "`nSETUP VALIDATION PASSED (windows not launched)" -ForegroundColor Green
    return
}

$bridgeScript = Join-Path $PSScriptRoot 'Start-DeswikTestBridge.ps1'
$testScript = Join-Path $PSScriptRoot 'Invoke-DeswikLiveTests.ps1'
$powerShellArgs = @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass')
$powerShellExe = (Get-Process -Id $PID).Path

Write-Host "`nStarting the bridge window" -ForegroundColor Cyan
$bridgeProcess = Start-Process -FilePath $powerShellExe -ArgumentList ($powerShellArgs + @(
    '-File', "`"$bridgeScript`"", '-RepoRoot', "`"$repoRoot`"", '-PythonPath', "`"$pythonExe`""
)) -PassThru
$deadline = [DateTime]::UtcNow.AddSeconds(15)
while ([DateTime]::UtcNow -lt $deadline -and -not (Test-TcpPortOpen -Port 9595)) {
    if ($bridgeProcess.HasExited) { throw 'The bridge stopped before opening port 9595.' }
    Start-Sleep -Milliseconds 250
}
if (-not (Test-TcpPortOpen -Port 9595)) { throw 'The bridge did not open port 9595 within 15 seconds.' }

Write-Host 'Opening the interactive test window' -ForegroundColor Cyan
Start-Process -FilePath $powerShellExe -ArgumentList ($powerShellArgs + @(
    '-NoExit', '-File', "`"$testScript`"", '-RepoRoot', "`"$repoRoot`""
)) | Out-Null

$addinPath = Join-Path $repoRoot 'deswik-mcp\src\Deswik.Addin\bin\Release\net8.0-windows\Deswik.Addin.dll'
Write-Host "`nREADY" -ForegroundColor Green
Write-Host 'The bridge and guided test windows are open.'
Write-Host "Load this add-in in Deswik.CAD:`n$addinPath" -ForegroundColor Yellow
