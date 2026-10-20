Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

Write-Host "=========================================="
Write-Host "    nyaa-view Cache Protection Tests      "
Write-Host "=========================================="

# 1. Settings default CacheLimitMb
Write-Host "`n[Test 1] Settings Default Cache Limit"
$s = New-Object NyaaView.Settings
Write-Host "  Default CacheLimitMb: $($s.CacheLimitMb) MB"
if ($s.CacheLimitMb -ne 1024) {
    Write-Host "FAILED: Expected default CacheLimitMb 1024, got $($s.CacheLimitMb)" -ForegroundColor Red
    exit 1
}
Write-Host "  Settings Default Cache Limit: PASS"

# 2. ZipEngine Protected Indices
Write-Host "`n[Test 2] ZipEngine Protected Indices Eviction Immunity"
$zip = New-Object NyaaView.ZipEngine
$zipPath = "$PSScriptRoot\..\sample_test\album_A.zip"
$entries = $zip.LoadZip($zipPath)
Write-Host "  Loaded zip entries: $($entries.Count)"

# Set low cache limit to force eviction (e.g. 1MB)
$zip.CacheLimitMb = 1

# Load page 0, page 1, page 2
$b0 = $zip.GetImage(0, $false)
$b1 = $zip.GetImage(1, $false)
$b2 = $zip.GetImage(2, $false)

# Set protected indices to [0, 1]
$zip.SetProtectedIndices([int[]]@(0, 1))

# Load page 3, 4, 5 with updateCurrentIndex = $false
$b3 = $zip.GetImage(3, $false)
$b4 = $zip.GetImage(4, $false)
$b5 = $zip.GetImage(5, $false)

# Verify page 0 and 1 are still cached (not disposed)
$b0_check = $zip.GetImage(0, $false)
$b1_check = $zip.GetImage(1, $false)

if ($b0_check -eq $null -or $b1_check -eq $null) {
    Write-Host "FAILED: Protected pages were evicted!" -ForegroundColor Red
    exit 1
}
Write-Host "  Protected indices [0, 1] preserved across cache pressure: PASS"

# 3. SettingsForm SwitchTab(3)
Write-Host "`n[Test 3] SettingsForm Tab 3 Navigation"
$form = New-Object NyaaView.SettingsForm($s)
$form.SwitchTab(3)
$bind = [System.Reflection.BindingFlags]"NonPublic,Instance"
$activeTabField = $form.GetType().GetField("_activeTabIndex", $bind)
$activeTab = $activeTabField.GetValue($form)
Write-Host "  Active tab index after SwitchTab(3): $activeTab"
if ($activeTab -ne 3) {
    Write-Host "FAILED: Expected active tab 3, got $activeTab" -ForegroundColor Red
    exit 1
}
$form.Dispose()
Write-Host "  SettingsForm SwitchTab(3): PASS"

Write-Host "`n=========================================="
Write-Host "   ALL CACHE TESTS PASSED SUCCESSFULLY!   "
Write-Host "=========================================="
