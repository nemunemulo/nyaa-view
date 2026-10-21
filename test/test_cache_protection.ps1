Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

$bind = [System.Reflection.BindingFlags]"NonPublic,Instance"

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

# 2. ZipEngine Protected Indices (Using PageCount mode with forward navigation)
Write-Host "`n[Test 2] ZipEngine Protected Indices Eviction Immunity"
$zip = New-Object NyaaView.ZipEngine
$zipPath = "$PSScriptRoot\..\sample_test\album_A.zip"
$entries = $zip.LoadZip($zipPath)
Write-Host "  Loaded zip entries: $($entries.Count)"

# Set CacheMode to PageCount and CacheWindow to 2
$zip.CacheMode = [NyaaView.CacheLimitMode]::PageCount
$zip.CacheWindow = 2

# Load page 0, page 1, page 2
$b0 = $zip.GetImage(0, $true)
$b1 = $zip.GetImage(1, $true)
$b2 = $zip.GetImage(2, $true)

# Set protected indices to [0, 1] (e.g. 2-page view currently displaying 0 & 1)
$zip.SetProtectedIndices([int[]]@(0, 1))

# Navigate forward to page 5 (CurrentIndex = 5).
# Window around 5 is [5 - 2, 5 + 2] = [3, 7].
# Page 2 is outside window (< 3) and UNPROTECTED -> MUST BE EVICTED.
# Pages 0 and 1 are outside window (< 3) but PROTECTED -> MUST NOT BE EVICTED!
$b3 = $zip.GetImage(3, $true)
$b4 = $zip.GetImage(4, $true)
$b5 = $zip.GetImage(5, $true)

# Inspect internal _cache dictionary via reflection
$cacheField = $zip.GetType().GetField("_cache", $bind)
$cacheDict = $cacheField.GetValue($zip)
Write-Host "  Cache entry count after moving to P.5: $($cacheDict.Count)"

# Verify eviction occurred: page 2 MUST have been evicted
$page2Cached = $cacheDict.ContainsKey(2)
Write-Host "  Unprotected Page 2 in cache: $page2Cached (Expected: False - Evicted)"
if ($page2Cached) {
    Write-Host "FAILED: Unprotected Page 2 should have been evicted by cache pressure!" -ForegroundColor Red
    exit 1
}

# Verify protected pages 0 and 1 were PRESERVED despite being outside window
$page0Cached = $cacheDict.ContainsKey(0)
$page1Cached = $cacheDict.ContainsKey(1)
Write-Host "  Protected Page 0 in cache: $page0Cached (Expected: True - Protected)"
Write-Host "  Protected Page 1 in cache: $page1Cached (Expected: True - Protected)"

if (-not $page0Cached -or -not $page1Cached) {
    Write-Host "FAILED: Protected pages were erroneously evicted!" -ForegroundColor Red
    exit 1
}
Write-Host "  Protected indices [0, 1] preserved while unprotected Page 2 was evicted: PASS"

# Also test MegaBytes mode with reflection-set low limit
Write-Host "`n[Test 2-B] Byte-based Eviction Immunity"
$zip.CacheMode = [NyaaView.CacheLimitMode]::MegaBytes
$limitField = $zip.GetType().GetField("_cacheLimitMb", $bind)
# Set private _cacheLimitMb to 0 (so any byte size forces eviction of unprotected pages)
$limitField.SetValue($zip, 0)

# Protect page 0
$zip.SetProtectedIndices([int[]]@(0))
$b7 = $zip.GetImage(7, $true)

# Verify page 0 is still in cache
$p0ByteCheck = $cacheDict.ContainsKey(0)
Write-Host "  Page 0 preserved under 0MB limit: $p0ByteCheck (Expected: True)"
if (-not $p0ByteCheck) {
    Write-Host "FAILED: Page 0 was evicted under byte pressure!" -ForegroundColor Red
    exit 1
}
Write-Host "  Byte-based eviction immunity: PASS"

# [Test 2-C] Target Image Immunity with updateCurrentIndex = $false (Webtoon Precache Scenario)
Write-Host "`n[Test 2-C] Target Image Immunity with updateCurrentIndex = `$false (Precache Scenario)"
$zipTestC = New-Object NyaaView.ZipEngine
$zipTestC.LoadZip($zipPath) | Out-Null
$zipTestC.CacheMode = [NyaaView.CacheLimitMode]::PageCount
$zipTestC.CacheWindow = 2

# Load page 0 as current page
$zipTestC.GetImage(0, $true) | Out-Null

# Now request page 5 with updateCurrentIndex = $false (distance 5 > window 2, far outside window)
$b5 = $zipTestC.GetImage(5, $false)
if ($b5 -eq $null) {
    Write-Host "FAILED: GetImage(5, `$false) returned null!" -ForegroundColor Red
    exit 1
}

# Access properties on b5 to ensure it was NOT disposed by CleanCache
try {
    $w = $b5.Width
    $h = $b5.Height
    Write-Host "  Requested Page 5 returned valid non-disposed bitmap: ${w}x${h}"
}
catch {
    Write-Host "FAILED: Returned bitmap was disposed by CleanCache: $_" -ForegroundColor Red
    exit 1
}

$cacheFieldC = $zipTestC.GetType().GetField("_cache", $bind)
$cacheDictC = $cacheFieldC.GetValue($zipTestC)
if (-not $cacheDictC.ContainsKey(5)) {
    Write-Host "FAILED: Page 5 should still be in cache immediately after retrieval!" -ForegroundColor Red
    exit 1
}
Write-Host "  Target Image Immunity under updateCurrentIndex = `$false: PASS"
$zipTestC.Close()

# 3. SettingsForm SwitchTab(3)
Write-Host "`n[Test 3] SettingsForm Tab 3 Navigation"
$form = New-Object NyaaView.SettingsForm($s)
$form.SwitchTab(3)
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
