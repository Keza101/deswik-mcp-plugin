[CmdletBinding()]
param(
    [string] $DeswikDir = $env:DESWIK_DIR,
    [string] $AddinDir = (Join-Path $PSScriptRoot '..\src\Deswik.Addin\bin\Release\net8.0-windows')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($DeswikDir)) {
    throw 'Set DESWIK_DIR or pass -DeswikDir with your Deswik.Suite installation folder.'
}

if (Get-Process Deswik.CAD -ErrorAction SilentlyContinue) {
    throw 'Close every Deswik.CAD process before preflight so the DLL and registration can be verified.'
}

$addinDirPath = (Resolve-Path $AddinDir).Path
$addinPath = Join-Path $addinDirPath 'Deswik.Addin.dll'
$bridgePath = Join-Path $addinDirPath 'Deswik.Bridge.dll'
$graphicsPath = Join-Path $DeswikDir 'Deswik.Graphics.dll'
foreach ($path in @($addinPath, $bridgePath, $graphicsPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required file: $path" }
}

$installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($graphicsPath).FileVersion
$resolver = [Func[System.Runtime.Loader.AssemblyLoadContext,Reflection.AssemblyName,Reflection.Assembly]] {
    param($context, $name)
    foreach ($directory in @($addinDirPath, $DeswikDir)) {
        $candidate = Join-Path $directory ($name.Name + '.dll')
        if (Test-Path -LiteralPath $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    }
    return $null
}

$loadContext = [System.Runtime.Loader.AssemblyLoadContext]::Default
$loadContext.add_Resolving($resolver)
try {
    $assembly = $loadContext.LoadFromAssemblyPath($addinPath)
    $type = $assembly.GetType('Deswik.Addin.DeswikMcpAddin', $true)
    $constructors = $type.GetConstructors()
    $checks = [ordered]@{
        'assembly version matches Deswik' = $assembly.GetName().Version.ToString() -eq $installedVersion
        'startup type is public' = $type.IsPublic
        'parameterless constructor exists' = @($constructors | Where-Object { $_.GetParameters().Count -eq 0 }).Count -eq 1
        'Application constructor exists' = @($constructors | Where-Object {
            $_.GetParameters().Count -eq 1 -and
            $_.GetParameters()[0].ParameterType.FullName -eq 'Deswik.Graphics.Application'
        }).Count -eq 1
        'Application property exists' = $null -ne $type.GetProperty('Application')
        'Load method exists' = @($type.GetMethods() | Where-Object Name -eq 'Load').Count -ge 1
        'Unload method exists' = @($type.GetMethods() | Where-Object Name -eq 'Unload').Count -eq 1
    }

    $instance = [Activator]::CreateInstance($type)
    $checks['startup type constructs'] = $null -ne $instance

    $registryPath = 'HKCU:\Software\Deswik\2025.2\Deswik.CAD\Plugins\Deswik.Addin'
    $registration = Get-ItemProperty -Path $registryPath
    $checks['registered directory matches build'] =
        [IO.Path]::GetFullPath($registration.FilePath).TrimEnd('\') -eq $addinDirPath.TrimEnd('\')
    $checks['startup class is exact'] = $registration.StartupClass -ceq 'DeswikMcpAddin'

    $binaryText = [Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes($addinPath))
    $checks['preview capability is in artifact'] = $binaryText.Contains('preview_ugdrillholes')
    $checks['rollback capability is in artifact'] = $binaryText.Contains('prepare_rollback_ugdrillholes')
    $guardType = $assembly.GetType('Deswik.Addin.GuardedWriteCoordinator', $true)
    $guardMethod = $guardType.GetMethod('RefreshDocumentIdentity',
        [Reflection.BindingFlags]'NonPublic,Instance')
    $guardIl = $guardMethod.GetMethodBody().GetILAsByteArray()
    $literals = for ($index = 0; $index -lt $guardIl.Length - 4; $index++) {
        if ($guardIl[$index] -eq 0x72) {
            $token = [BitConverter]::ToInt32($guardIl, $index + 1)
            try { $guardMethod.Module.ResolveString($token) } catch { }
        }
    }
    $checks['unsaved drawing guard is in artifact'] = @($literals).Contains('drawing_unsaved')

    $failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
    foreach ($check in $checks.GetEnumerator()) {
        $status = if ($check.Value) { 'PASS' } else { 'FAIL' }
        Write-Host "$status $($check.Key)"
    }
    if ($failed.Count -gt 0) { throw "$($failed.Count) add-in preflight check(s) failed." }
    Write-Host "ALL ADD-IN PREFLIGHT CHECKS PASSED ($($checks.Count))"
}
finally {
    $loadContext.remove_Resolving($resolver)
}
