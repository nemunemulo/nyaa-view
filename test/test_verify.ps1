Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

Write-Host "=========================================="
Write-Host "      nyaa-view Comprehensive Verification"
Write-Host "=========================================="

$failed = $false

function Assert-Condition($condition, $message) {
    if (-not $condition) {
        Write-Host "  [FAIL] $message" -ForegroundColor Red
        $script:failed = $true
    } else {
        Write-Host "  [PASS] $message" -ForegroundColor Green
    }
}

# 1. Settings Save & Load Test
Write-Host "`n[1] Testing Settings default values & persistence"
$sDefault = New-Object NyaaView.Settings
Assert-Condition ($sDefault.CacheMode -eq [NyaaView.CacheLimitMode]::MegaBytes) "Default CacheMode is MegaBytes"
Assert-Condition ($sDefault.CacheLimitMb -eq 1024) "Default CacheLimitMb is 1024"
Assert-Condition ($sDefault.WebtoonSmoothScroll -eq $true) "Default WebtoonSmoothScroll is true"

$settings = [NyaaView.Settings]::Load()

$settings.NextKeys.Add([System.Windows.Forms.Keys]::Space)
$settings.WheelMode = 1
$settings.WheelInvert = $true
$settings.ViewMode = [NyaaView.ViewLayout]::DualRtl
$settings.CacheLimitMb = 2048
$settings.Save()

$reloaded = [NyaaView.Settings]::Load()
Assert-Condition ($reloaded.NextKeys.Contains([System.Windows.Forms.Keys]::Space)) "Persist NextKeys (Space)"
Assert-Condition ($reloaded.WheelMode -eq 1) "Persist WheelMode"
Assert-Condition ($reloaded.ViewMode -eq [NyaaView.ViewLayout]::DualRtl) "Persist ViewMode (DualRtl)"
Assert-Condition ($reloaded.CacheLimitMb -eq 2048) "Persist CacheLimitMb (2048)"

$reloaded.ResetToDefaults()
$reloaded.Save()
Write-Host "  Settings successfully reset to clean defaults."

# 2. Testing SystemMemoryHelper
Write-Host "`n[2] Testing SystemMemoryHelper RAM calculation"
$ramGb = [NyaaView.SystemMemoryHelper]::GetInstalledRamGb()
$ramMb = [NyaaView.SystemMemoryHelper]::GetTotalPhysicalMemoryMb()
$halfMb = [NyaaView.SystemMemoryHelper]::GetHalfPhysicalMemoryMb()
Write-Host "  Detected Installed RAM: $ramGb GB ($ramMb MB), Half: $halfMb MB"
Assert-Condition ($ramMb -gt 512 -and $halfMb -gt 256) "Physical RAM detection returns positive valid values"

# 3. Testing FilterEngine with 1x1 & odd-width bitmaps (Stride alignment & IndexOutOfRange defense)
Write-Host "`n[3] Testing FilterEngine (Stride alignment & bounds safety)"
try {
    # 1x1 24bpp bitmap (Row stride is 4 bytes, actual pixel data is 3 bytes)
    $bmp1x1 = New-Object System.Drawing.Bitmap 1, 1, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $bmp1x1.SetPixel(0, 0, [System.Drawing.Color]::FromArgb(100, 150, 200))

    $inv1x1 = [NyaaView.FilterEngine]::ApplyFilter($bmp1x1, [NyaaView.FilterType]::Invert)
    Assert-Condition ($inv1x1 -ne $null) "Invert filter on 1x1 image succeeds without IndexOutOfRangeException"
    $c = $inv1x1.GetPixel(0, 0)
    Assert-Condition ($c.R -eq (255 - 100) -and $c.G -eq (255 - 150) -and $c.B -eq (255 - 200)) "Invert filter correctly computes 1x1 inverted color"

    $clean1x1 = [NyaaView.FilterEngine]::ApplyFilter($bmp1x1, [NyaaView.FilterType]::MangaClean)
    Assert-Condition ($clean1x1 -ne $null) "MangaClean filter on 1x1 image succeeds without IndexOutOfRangeException"

    # Odd width bitmap (13x7) - Tests padding handling across multiple rows
    $bmpOdd = New-Object System.Drawing.Bitmap 13, 7, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $invOdd = [NyaaView.FilterEngine]::ApplyFilter($bmpOdd, [NyaaView.FilterType]::Invert)
    Assert-Condition ($invOdd -ne $null) "Invert filter on 13x7 odd dimension image succeeds"

    $bmp1x1.Dispose()
    $inv1x1.Dispose()
    $clean1x1.Dispose()
    $bmpOdd.Dispose()
    $invOdd.Dispose()
} catch {
    Assert-Condition $false "FilterEngine threw exception: $($_.Exception.Message)"
}

# 4. Testing ImageLoader size limit defense
Write-Host "`n[4] Testing ImageLoader oversized stream safety"
$tooLargeStream = New-Object System.IO.MemoryStream
# Create a dummy stream over 256MB would use memory, test with a custom stream or verify MaxStreamBytes constant
$maxBytes = [NyaaView.ImageLoader]::MaxStreamBytes
Assert-Condition ($maxBytes -eq 268435456) "MaxStreamBytes is configured to 256MB"

# 5. Testing ZIP Engine & Metadata Extraction on sample_test/album_A.zip
Write-Host "`n[5] Testing ZipEngine on sample_test/album_A.zip"
$zipPath = "$PSScriptRoot\..\sample_test\album_A.zip"
Assert-Condition (Test-Path $zipPath) "Sample test ZIP exists ($zipPath)"

$zipEngine = New-Object NyaaView.ZipEngine
$zipEngine.CacheMode = [NyaaView.CacheLimitMode]::MegaBytes
$zipEngine.CacheLimitMb = 16
$entries = $zipEngine.LoadZip($zipPath)
Assert-Condition ($entries.Count -gt 0) "Successfully loaded ZIP with $($entries.Count) images"

for ($i = 0; $i -lt $entries.Count; $i++) {
    $bmp = $zipEngine.GetImage($i, $true)
    $name = $zipEngine.GetEntryName($i)
    $size = $zipEngine.GetEntrySize($i)
    $date = $zipEngine.GetEntryDateTime($i)
    $isGif = $name.EndsWith(".gif", [StringComparison]::OrdinalIgnoreCase)

    $info = [NyaaView.MetadataEngine]::Extract($bmp, $name, "", $zipPath, $size, $date, $isGif, 1, 0, 0)
    Assert-Condition ($info -ne $null -and $info.Width -gt 0) "Image [$i] $name extracted resolution: $($info.Width)x$($info.Height)"
}
$zipEngine.Close()

Write-Host "`n=========================================="
if ($failed) {
    Write-Host "  VERIFICATION FAILED: Some tests did not pass!" -ForegroundColor Red
    exit 1
} else {
    Write-Host "  ALL TESTS PASSED! Verification Complete." -ForegroundColor Green
    exit 0
}
