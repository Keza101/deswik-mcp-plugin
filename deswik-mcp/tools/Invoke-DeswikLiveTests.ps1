[CmdletBinding()]
param([Parameter(Mandatory)] [string] $RepoRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$host.UI.RawUI.WindowTitle = 'Deswik Points collection geometry acceptance'
Set-Location -LiteralPath $RepoRoot
. (Join-Path $RepoRoot 'deswik-mcp\tools\deswik.ps1')

$script:testFailures = 0
$script:testCount = 0
$stressIterations = 50

function Test-LiveCheck {
    param([Parameter(Mandatory)] [string] $Name, [Parameter(Mandatory)] [bool] $Pass)
    $script:testCount++
    if ($Pass) { Write-Host "PASS $Name" -ForegroundColor Green }
    else { Write-Host "FAIL $Name" -ForegroundColor Red; $script:testFailures++ }
}

function Wait-ForCadRead {
    while ($true) {
        try { return dsw get_ug_selection_context }
        catch {
            Write-Host "CAD read is not ready: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host 'Open Deswik.CAD, load the newly built Deswik.Addin.dll, and open a saved disposable drawing.'
            $answer = Read-Host 'Press Enter to retry, or type Q to stop'
            if ($answer -match '^[Qq]$') { throw 'Live acceptance cancelled.' }
        }
    }
}

function Test-FiniteValue($Value) {
    return $null -ne $Value -and [double]::IsFinite([double]$Value)
}

function Test-FinitePoint($Point) {
    return $null -ne $Point -and (Test-FiniteValue $Point.x) -and
        (Test-FiniteValue $Point.y) -and (Test-FiniteValue $Point.z)
}

function Get-AttributeSignature($Attributes) {
    if ($null -eq $Attributes) { return '' }
    return (@($Attributes.PSObject.Properties | Sort-Object Name | ForEach-Object {
        $_.Name + '=' + ($_.Value | ConvertTo-Json -Depth 8 -Compress)
    }) -join '|')
}

Write-Host 'POINTS COLLECTION GEOMETRY LIVE ACCEPTANCE' -ForegroundColor Cyan
Write-Host "This suite exercises page boundaries, refusal paths, and $stressIterations repeated reads."
Write-Host 'It is read-only and must not create or modify drawing entities.'
Write-Host ''
Write-Host 'Prepare one saved disposable drawing containing:' -ForegroundColor Yellow
Write-Host '  - one native Points collection containing 3 to 20 known points'
Write-Host '  - one single Point and one Polyline for wrong-type refusal checks'
Write-Host 'Do not substitute several separate Point entities for one Points collection.'
Write-Host 'Use known coordinates and display settings so you can compare the result in Properties.'
Write-Host 'Press Esc until no figures are selected.'
Read-Host 'Press Enter when the empty-selection check is ready'

$empty = Wait-ForCadRead
while (@($empty.sourceHandles).Count -ne 0) {
    Write-Host 'CAD still has selected figures. Press Esc in CAD to clear them.' -ForegroundColor Yellow
    Read-Host 'Press Enter to check again'
    $empty = Wait-ForCadRead
}
Test-LiveCheck 'empty selection remains empty' (@($empty.sourceHandles).Count -eq 0)

$missing = dsw get_cad_points_geometry @{} -RawResponse
$zero = dsw get_cad_points_geometry @{ handle=0 } -RawResponse
$extra = dsw get_cad_points_geometry @{ handle=1; extra=$true } -RawResponse
$unknown = dsw get_cad_points_geometry @{ handle=[uint64]::MaxValue } -RawResponse
$negativeStart = dsw get_cad_points_geometry @{ handle=1; start=-1 } -RawResponse
$zeroLimit = dsw get_cad_points_geometry @{ handle=1; limit=0 } -RawResponse
$largeLimit = dsw get_cad_points_geometry @{ handle=1; limit=501 } -RawResponse
$stringLimit = dsw get_cad_points_geometry @{ handle=1; limit='5' } -RawResponse
Test-LiveCheck 'missing handle refused' (-not $missing.success -and $missing.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'zero handle refused' (-not $zero.success -and $zero.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'extra parameter refused' (-not $extra.success -and $extra.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'unknown handle refused' (-not $unknown.success -and $unknown.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'negative start refused' (-not $negativeStart.success -and $negativeStart.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'zero limit refused' (-not $zeroLimit.success -and $zeroLimit.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'limit above 500 refused' (-not $largeLimit.success -and $largeLimit.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'string limit refused' (-not $stringLimit.success -and $stringLimit.errorCode -eq 'invalid_geometry_request')

Write-Host "`nSelect exactly the test Points collection, one single Point, and one Polyline." -ForegroundColor Yellow
Read-Host 'Press Enter after the fixtures are selected'
$context = dsw get_ug_selection_context
while ($true) {
    $presentTypes = @($context.figures.type | Sort-Object -Unique)
    $missingTypes = @(@('Points', 'Point', 'Polyline') | Where-Object { $_ -notin $presentTypes })
    $exactFixtures = @($context.figures).Count -eq 3 -and
        @($context.figures | Where-Object type -eq 'Points').Count -eq 1 -and
        @($context.figures | Where-Object type -eq 'Point').Count -eq 1 -and
        @($context.figures | Where-Object type -eq 'Polyline').Count -eq 1
    if ($missingTypes.Count -eq 0 -and $exactFixtures) { break }
    if ($missingTypes.Count) { Write-Host "Missing selected fixture types: $($missingTypes -join ', ')" -ForegroundColor Yellow }
    if (-not $exactFixtures) { Write-Host 'Select only one Points collection, one Point, and one Polyline.' -ForegroundColor Yellow }
    Read-Host 'Correct the selection, then press Enter'
    $context = dsw get_ug_selection_context
}

$pointsFigure = @($context.figures | Where-Object type -eq 'Points')[0]
$pointFigure = @($context.figures | Where-Object type -eq 'Point')[0]
$polyline = @($context.figures | Where-Object type -eq 'Polyline')[0]
$handle = [uint64]$pointsFigure.handle
$page0 = dsw get_cad_points_geometry @{ handle=$handle; start=0; limit=1 }
while ([int]$page0.pointCount -lt 3 -or [int]$page0.pointCount -gt 20) {
    Write-Host "The selected Points collection has $($page0.pointCount) points; use a fixture with 3 to 20." -ForegroundColor Yellow
    Read-Host 'Select the replacement Points collection plus the same Point and Polyline, then press Enter'
    $context = dsw get_ug_selection_context
    $exactFixtures = @($context.figures).Count -eq 3 -and
        @($context.figures | Where-Object type -eq 'Points').Count -eq 1 -and
        @($context.figures | Where-Object type -eq 'Point').Count -eq 1 -and
        @($context.figures | Where-Object type -eq 'Polyline').Count -eq 1
    if (-not $exactFixtures) { Write-Host 'Selection must contain exactly the three requested fixtures.' -ForegroundColor Yellow; continue }
    $pointsFigure = @($context.figures | Where-Object type -eq 'Points')[0]
    if ($null -eq $pointsFigure) { continue }
    $handle = [uint64]$pointsFigure.handle
    $page0 = dsw get_cad_points_geometry @{ handle=$handle; start=0; limit=1 }
}
$pointFigure = @($context.figures | Where-Object type -eq 'Point')[0]
$polyline = @($context.figures | Where-Object type -eq 'Polyline')[0]
$beforeLayers = dsw get_cad_layers | Sort-Object name | Select-Object name,entityCount | ConvertTo-Json -Compress
$beforeDocument = dsw get_cad_document
$sourceHandles = @($context.sourceHandles | ForEach-Object { [uint64]$_ } | Sort-Object)
$pointCount = [int]$page0.pointCount
$page1 = dsw get_cad_points_geometry @{ handle=$handle; start=1; limit=1 }
$last = dsw get_cad_points_geometry @{ handle=$handle; start=($pointCount-1); limit=500 }
$beyond = dsw get_cad_points_geometry @{ handle=$handle; start=($pointCount+1); limit=500 }
$all = dsw get_cad_points_geometry @{ handle=$handle; start=0; limit=500 }
$hex = dsw get_cad_points_geometry @{ handle=('0x{0:X}' -f $handle); start=0; limit=500 }

Test-LiveCheck 'exact Points handle returned' ([uint64]$page0.handle -eq $handle)
Test-LiveCheck 'schema version is one' ($page0.schemaVersion -eq 1)
Test-LiveCheck 'raw status remains unverified' (-not $page0.readyForDesign -and $page0.units -eq 'unknown' -and $page0.coordinateSystem -eq 'unknown')
Test-LiveCheck 'point count is stable across pages' ($page1.pointCount -eq $pointCount -and $last.pointCount -eq $pointCount -and $all.pointCount -eq $pointCount)
Test-LiveCheck 'first page contains exactly one finite point' (@($page0.points).Count -eq 1 -and (Test-FinitePoint $page0.points[0]))
Test-LiveCheck 'second page contains exactly one finite point' (@($page1.points).Count -eq 1 -and (Test-FinitePoint $page1.points[0]))
Test-LiveCheck 'first page advances to one' ($page0.nextStart -eq 1)
Test-LiveCheck 'second page advances to two' ($page1.nextStart -eq 2)
Test-LiveCheck 'final page contains one point and stops' (@($last.points).Count -eq 1 -and $null -eq $last.nextStart)
Test-LiveCheck 'beyond-end page is empty and stops' (@($beyond.points).Count -eq 0 -and $null -eq $beyond.nextStart)
Test-LiveCheck 'maximum page returns every small-fixture point' (@($all.points).Count -eq $pointCount -and $null -eq $all.nextStart)
Test-LiveCheck 'every returned point is finite' (@($all.points | Where-Object { -not (Test-FinitePoint $_) }).Count -eq 0)
Test-LiveCheck 'decimal and hexadecimal handles agree' (($all | ConvertTo-Json -Depth 12 -Compress) -eq ($hex | ConvertTo-Json -Depth 12 -Compress))
Test-LiveCheck 'display metadata is finite' ((Test-FiniteValue $all.alignToViewSize) -and (Test-FinitePoint $all.extrusionVector))
Test-LiveCheck 'display metadata is stable across pages' ($page0.isPointCloud -eq $page1.isPointCloud -and $page0.pointStyle -eq $page1.pointStyle -and $page0.sizeType -eq $page1.sizeType -and $page0.alignToView -eq $page1.alignToView -and $page0.alignToViewSize -eq $page1.alignToViewSize)

$stable = $true
$allJson = $all | ConvertTo-Json -Depth 12 -Compress
for ($iteration = 1; $iteration -le $stressIterations; $iteration++) {
    $again = dsw get_cad_points_geometry @{ handle=$handle; start=0; limit=500 }
    if (($again | ConvertTo-Json -Depth 12 -Compress) -ne $allJson) { $stable = $false; break }
}
Test-LiveCheck "full page stable across $stressIterations repeated reads" $stable

$singlePointWrong = dsw get_cad_points_geometry @{ handle=[uint64]$pointFigure.handle } -RawResponse
$polylineWrong = dsw get_cad_points_geometry @{ handle=[uint64]$polyline.handle } -RawResponse
$pointsAsPolyline = dsw get_cad_polyline_geometry @{ handle=$handle } -RawResponse
$pointsAsSimple = dsw get_cad_figure_geometry @{ handle=$handle } -RawResponse
Test-LiveCheck 'single Point refused by Points reader' (-not $singlePointWrong.success -and $singlePointWrong.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'Polyline refused by Points reader' (-not $polylineWrong.success -and $polylineWrong.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'Points collection refused by Polyline reader' (-not $pointsAsPolyline.success -and $pointsAsPolyline.errorCode -eq 'invalid_geometry_request')
Test-LiveCheck 'Points collection refused by simple-figure reader' (-not $pointsAsSimple.success -and $pointsAsSimple.errorCode -eq 'invalid_geometry_request')

$afterContext = dsw get_ug_selection_context
$afterHandles = @($afterContext.sourceHandles | ForEach-Object { [uint64]$_ } | Sort-Object)
$afterLayers = dsw get_cad_layers | Sort-Object name | Select-Object name,entityCount | ConvertTo-Json -Compress
$afterDocument = dsw get_cad_document
Test-LiveCheck 'selection handles unchanged' (($sourceHandles -join ',') -eq ($afterHandles -join ','))
Test-LiveCheck 'layer entity counts unchanged' ($beforeLayers -eq $afterLayers)
Test-LiveCheck 'drawing dirty state unchanged' ($beforeDocument.isDirty -eq $afterDocument.isDirty)

Write-Host "`nMANUAL CAD COMPARISON" -ForegroundColor Cyan
Write-Host "Returned point count: $pointCount; style: $($all.pointStyle); size type: $($all.sizeType); align to view: $($all.alignToView)" -ForegroundColor Yellow
$pointRows = for ($index = 0; $index -lt @($all.points).Count; $index++) {
    [pscustomobject]@{ Index=$index; X=$all.points[$index].x; Y=$all.points[$index].y; Z=$all.points[$index].z }
}
$pointRows | Format-Table
Write-Host 'Compare the count, coordinates, and display settings with CAD Properties.' -ForegroundColor Yellow
Write-Host 'Confirm no geometry moved, appeared, disappeared, or changed and the dirty indicator stayed unchanged.' -ForegroundColor Yellow
$manual = Read-Host 'Did all visual comparisons match? Type Y for yes'
Test-LiveCheck 'manual CAD geometry comparison' ($manual -match '^[Yy]$')

Write-Host "`nChecks run: $script:testCount" -ForegroundColor Cyan
Write-Host "Failures:   $script:testFailures" -ForegroundColor $(if ($script:testFailures) { 'Red' } else { 'Green' })
if ($script:testFailures -eq 0) { Write-Host 'POINTS COLLECTION GEOMETRY ACCEPTANCE PASSED' -ForegroundColor Green }
else { Write-Host 'Acceptance did not pass. Copy the FAIL lines before closing this window.' -ForegroundColor Red }
