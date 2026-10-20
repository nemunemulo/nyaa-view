Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

Write-Host "=========================================="
Write-Host "    nyaa-view Drag & Drop Index Tests     "
Write-Host "=========================================="

$m = New-Object NyaaView.MainForm
$bind = [System.Reflection.BindingFlags]"NonPublic,Instance"
$currIdxField = $m.GetType().GetField("_currentIndex", $bind)
$scrollField = $m.GetType().GetField("_webtoonScrollY", $bind)

# 1. Single Image Mode test with middle file
Write-Host "`n[Test 1] Regular View Mode: Loading middle file"
$sampleDir = "$PSScriptRoot\..\sample_test"
$bmpPath = "$sampleDir\scenery_view.bmp"
# Also test with sample folder files
$testFolder = "$PSScriptRoot\test_images"
if (Test-Path $testFolder) { Remove-Item $testFolder -Recurse -Force }
New-Item -ItemType Directory -Path $testFolder | Out-Null

# Create 5 small bitmaps: img_01.bmp ... img_05.bmp
for ($i = 1; $i -le 5; $i++) {
    $bmp = New-Object System.Drawing.Bitmap 100, 100
    $p = Join-Path $testFolder ("img_0{0}.bmp" -f $i)
    $bmp.Save($p)
    $bmp.Dispose()
}

# Drag / load middle file: img_03.bmp
$middleFile = Join-Path $testFolder "img_03.bmp"
$m.LoadSingleImage($middleFile)

$idx = $currIdxField.GetValue($m)
Write-Host "  Loaded middle file: $middleFile -> Index: $idx (Expected: 2)"
if ($idx -ne 2) {
    Write-Host "FAILED: Expected index 2, got $idx" -ForegroundColor Red
    exit 1
}
Write-Host "  Regular View Mode Middle File: PASS"

# 2. Case-insensitivity test (IMG_03.BMP uppercase)
Write-Host "`n[Test 2] Case-insensitive matching (uppercase path)"
$upperFile = (Join-Path $testFolder "IMG_03.BMP").ToUpper()
$m.LoadSingleImage($upperFile)
$idxUpper = $currIdxField.GetValue($m)
Write-Host "  Loaded uppercase path -> Index: $idxUpper (Expected: 2)"
if ($idxUpper -ne 2) {
    Write-Host "FAILED: Case mismatch caused index failure, got $idxUpper" -ForegroundColor Red
    exit 1
}
Write-Host "  Case-insensitive matching: PASS"

# 3. Webtoon Mode Middle File Scroll Positioning
Write-Host "`n[Test 3] Webtoon Mode: Middle File Auto-Scroll Position"
$m.SetViewMode([NyaaView.ViewLayout]::Webtoon)
$m.LoadSingleImage($middleFile)

$idxWebtoon = $currIdxField.GetValue($m)
$scrollY = $scrollField.GetValue($m)
Write-Host "  Webtoon Mode Loaded Middle File -> Index: $idxWebtoon, ScrollY: $scrollY"

if ($idxWebtoon -ne 2) {
    Write-Host "FAILED: Webtoon index is not 2" -ForegroundColor Red
    exit 1
}
if ($scrollY -le 0) {
    Write-Host "FAILED: Webtoon ScrollY was not scrolled to middle file (ScrollY: $scrollY)" -ForegroundColor Red
    exit 1
}
Write-Host "  Webtoon Auto-Scroll to Target Index: PASS"

# Clean up
Remove-Item $testFolder -Recurse -Force
$m.Dispose()

Write-Host "`n=========================================="
Write-Host "   ALL DRAG & DROP TESTS PASSED! (3/3)    "
Write-Host "=========================================="
