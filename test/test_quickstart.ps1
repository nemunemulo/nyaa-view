Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

Write-Host "=========================================="
Write-Host "     nyaa-view QuickStartForm Tests       "
Write-Host "=========================================="

# 1. QuickStartForm instantiation
Write-Host "`n[Test 1] Form Instantiation & RAM Detection"
$s = New-Object NyaaView.Settings
$s.FirstRunTutorialShown = $false
$s.WheelMode = 1
$s.CacheLimitMb = 1024

$form = New-Object NyaaView.QuickStartForm($s)
Write-Host "  Form created successfully: $($form.Text)"

$bind = [System.Reflection.BindingFlags]"NonPublic,Instance"
$rbWheelPage = $form.GetType().GetField("_rbWheelPageFlip", $bind).GetValue($form)
$rbWheelZoom = $form.GetType().GetField("_rbWheelZoom", $bind).GetValue($form)
$rbCache1024 = $form.GetType().GetField("_rbCache1024", $bind).GetValue($form)
$rbCache2048 = $form.GetType().GetField("_rbCache2048", $bind).GetValue($form)

if (-not $rbWheelPage.Checked) {
    Write-Host "FAILED: Expected _rbWheelPageFlip to be checked" -ForegroundColor Red
    exit 1
}
Write-Host "  _rbWheelPageFlip checked as expected: PASS"

if (-not $rbCache1024.Checked) {
    Write-Host "FAILED: Expected _rbCache1024 to be checked" -ForegroundColor Red
    exit 1
}
Write-Host "  _rbCache1024 checked as expected: PASS"

# 2. Simulate User Change to Zoom and 2048 MB
Write-Host "`n[Test 2] Setting Modifications and Start Action"
$rbWheelZoom.Checked = $true
$rbCache2048.Checked = $true

$onStartMethod = $form.GetType().GetMethod("OnStartClicked", $bind)
$onStartMethod.Invoke($form, @())

$updated = $form.GetUpdatedSettings()
Write-Host "  Updated WheelMode: $($updated.WheelMode) (Expected: 0 for Zoom)"
Write-Host "  Updated CacheLimitMb: $($updated.CacheLimitMb) (Expected: 2048)"
Write-Host "  Updated FirstRunTutorialShown: $($updated.FirstRunTutorialShown) (Expected: True)"

if ($updated.WheelMode -ne 0) {
    Write-Host "FAILED: Expected WheelMode 0" -ForegroundColor Red
    exit 1
}
if ($updated.CacheLimitMb -ne 2048) {
    Write-Host "FAILED: Expected CacheLimitMb 2048" -ForegroundColor Red
    exit 1
}
if ($updated.FirstRunTutorialShown -ne $true) {
    Write-Host "FAILED: Expected FirstRunTutorialShown True" -ForegroundColor Red
    exit 1
}

$form.Dispose()
Write-Host "  QuickStartForm save and update logic: PASS"

Write-Host "`n=========================================="
Write-Host "  ALL QUICK START TESTS PASSED! (2/2)     "
Write-Host "=========================================="
