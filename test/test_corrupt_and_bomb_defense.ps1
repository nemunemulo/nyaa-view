Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

$bind = [System.Reflection.BindingFlags]"NonPublic,Instance"

# Suppress UI message box popups during automated test
[NyaaView.MainForm]::SuppressDialogsForTesting = $true

Write-Host "=========================================="
Write-Host " nyaa-view Corrupt & Bomb Defense Tests   "
Write-Host "=========================================="

# Test 1: ImageLoader Pixel Bomb / Dimension Validation
Write-Host "`n[Test 1] Pixel Bomb & Dimension Validation"
$val1 = [NyaaView.ImageLoader]::ValidateDimensions(65536, 100) # Exceeds MaxDimension (65535)
$val2 = [NyaaView.ImageLoader]::ValidateDimensions(20000, 20000) # 400 MP, exceeds MaxPixelCount (100 MP)
$val3 = [NyaaView.ImageLoader]::ValidateDimensions(-10, 500) # Invalid negative dimension
$val4 = [NyaaView.ImageLoader]::ValidateDimensions(0, 500) # Zero dimension
$val5 = [NyaaView.ImageLoader]::ValidateDimensions(4000, 3000) # Normal 12 MP
$val6 = [NyaaView.ImageLoader]::ValidateDimensions(1500, 50000) # 75 MP Webtoon strip

Write-Host "  65536x100 valid: $val1 (Expected: False)"
Write-Host "  20000x20000 valid: $val2 (Expected: False)"
Write-Host "  -10x500 valid: $val3 (Expected: False)"
Write-Host "  0x500 valid: $val4 (Expected: False)"
Write-Host "  4000x3000 valid: $val5 (Expected: True)"
Write-Host "  1500x50000 valid: $val6 (Expected: True)"

if ($val1 -or $val2 -or $val3 -or $val4 -or (-not $val5) -or (-not $val6)) {
    Write-Host "FAILED: Dimension validation checks failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  Pixel Bomb / Dimension Validation: PASS"

# Test 2: Corrupted ZIP File Handling
Write-Host "`n[Test 2] Corrupted ZIP File Handling"
$tempCorruptZip = [System.IO.Path]::GetTempFileName() + ".zip"
[System.IO.File]::WriteAllBytes($tempCorruptZip, [byte[]]@(0x50, 0x4B, 0x03, 0x04, 0x00, 0x11, 0x22, 0x33)) # Truncated broken zip header

$main = New-Object NyaaView.MainForm
$main.CreateControl()
$handle = $main.Handle

try {
    # LoadArchive should not throw uncaught exception
    $main.LoadArchive($tempCorruptZip, 0)
    Write-Host "  LoadArchive on broken ZIP completed without unhandled crash: PASS"
}
catch {
    Write-Host "FAILED: LoadArchive crashed on broken ZIP: $_" -ForegroundColor Red
    exit 1
}
finally {
    if (Test-Path $tempCorruptZip) { Remove-Item $tempCorruptZip -Force }
}

# Test 3: ZIP with Corrupted Image Entry inside
Write-Host "`n[Test 3] ZIP with Corrupted Image Entry inside"
$tempTestZip = [System.IO.Path]::GetTempFileName() + ".zip"
$tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.IO.Path]::GetRandomFileName())
[System.IO.Directory]::CreateDirectory($tempDir) | Out-Null

try {
    # 1. Create page1.bmp (valid)
    $bmp1 = New-Object System.Drawing.Bitmap 100, 100
    $g1 = [System.Drawing.Graphics]::FromImage($bmp1)
    $g1.Clear([System.Drawing.Color]::Blue)
    $g1.Dispose()
    $p1 = [System.IO.Path]::Combine($tempDir, "01_good.bmp")
    $bmp1.Save($p1, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp1.Dispose()

    # 2. Create page2.jpg (corrupt bytes)
    $p2 = [System.IO.Path]::Combine($tempDir, "02_corrupt.jpg")
    [System.IO.File]::WriteAllBytes($p2, [byte[]]@(0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x00, 0x00, 0xDE, 0xAD, 0xBE, 0xEF))

    # 3. Create page3.bmp (valid)
    $bmp3 = New-Object System.Drawing.Bitmap 100, 100
    $g3 = [System.Drawing.Graphics]::FromImage($bmp3)
    $g3.Clear([System.Drawing.Color]::Green)
    $g3.Dispose()
    $p3 = [System.IO.Path]::Combine($tempDir, "03_good.bmp")
    $bmp3.Save($p3, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp3.Dispose()

    # Create zip
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $tempTestZip)

    # Load into MainForm
    $settingsField = $main.GetType().GetField("_settings", $bind)
    $settings = $settingsField.GetValue($main)
    $settings.ViewMode = [NyaaView.ViewLayout]::Single

    $main.LoadArchive($tempTestZip, 0)
    
    $rawBmpField = $main.GetType().GetField("_rawBitmap", $bind)
    $statusLblField = $main.GetType().GetField("_statusLabel", $bind)
    $statusLbl = $statusLblField.GetValue($main)

    # Page 1: good
    $raw1 = $rawBmpField.GetValue($main)
    Write-Host "  Page 0 (Good) loaded: $($raw1.Width)x$($raw1.Height)"
    if ($raw1.Width -ne 100) {
        Write-Host "FAILED: Expected 100x100 for page 1" -ForegroundColor Red
        exit 1
    }

    # Next -> Page 2 (Corrupt)
    $nextMethod = $main.GetType().GetMethod("NextImage", $bind)
    $nextMethod.Invoke($main, $null)

    $raw2 = $rawBmpField.GetValue($main)
    Write-Host "  Page 1 (Corrupt) handled: rawBitmap = $(if ($raw2 -ne $null) { 'Placeholder (' + $raw2.Width + 'x' + $raw2.Height + ')' } else { 'Null' })"
    Write-Host "  Status text: $($statusLbl.Text)"

    if ($raw2 -eq $null -or $raw2.Width -ne 800 -or -not ($statusLbl.Text -like "*02_corrupt.jpg*")) {
        Write-Host "FAILED: Corrupted image did not produce damaged image placeholder!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Damaged Image Placeholder displayed cleanly: PASS"

    # Next -> Page 3 (Good)
    $nextMethod.Invoke($main, $null)
    $raw3 = $rawBmpField.GetValue($main)
    Write-Host "  Page 2 (Good) loaded after corrupt page: $($raw3.Width)x$($raw3.Height)"
    if ($raw3.Width -ne 100) {
        Write-Host "FAILED: Expected 100x100 for page 3" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Continuous browsing across damaged entry: PASS"
}
finally {
    $main.Dispose()
    if (Test-Path $tempTestZip) { Remove-Item $tempTestZip -Force }
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
}

Write-Host "`n=========================================="
Write-Host " ALL CORRUPT & BOMB TESTS PASSED! (3/3)   "
Write-Host "=========================================="
