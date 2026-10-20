Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

$bind = [System.Reflection.BindingFlags]"NonPublic,Instance"

Write-Host "=========================================="
Write-Host "     nyaa-view Webtoon Bug Fix Tests      "
Write-Host "=========================================="

# Test 1: ToggleAutoHide in Webtoon Mode
Write-Host "`n[Test 1] ToggleAutoHide in Webtoon mode without exception"
$f = New-Object NyaaView.MainForm
$f.CreateControl()
$handle = $f.Handle
$settingsField = $f.GetType().GetField("_settings", $bind)
$settings = $settingsField.GetValue($f)
$settings.ViewMode = [NyaaView.ViewLayout]::Webtoon

# Load a sample file
$sampleBmp = "$PSScriptRoot\..\sample_test\scenery_view.bmp"
$f.LoadSingleImage($sampleBmp)

# Toggle AutoHide multiple times
$toggleMethod = $f.GetType().GetMethod("ToggleAutoHide", $bind)
try {
    for ($i = 0; $i -lt 4; $i++) {
        $toggleMethod.Invoke($f, @())
    }
    Write-Host "  ToggleAutoHide (4x toggles): PASS (No exceptions thrown)"
} catch {
    Write-Host "  FAILED: ToggleAutoHide threw $($_.Exception)" -ForegroundColor Red
    exit 1
}

# Test 2: Middle file Drag & Drop in folder with mixed ratios (wide banner + tall pages)
Write-Host "`n[Test 2] Loading middle file with mixed aspect ratios"
$testMixedDir = "$PSScriptRoot\test_mixed_ratios"
if (Test-Path $testMixedDir) { Remove-Item $testMixedDir -Recurse -Force }
New-Item -ItemType Directory -Path $testMixedDir | Out-Null

# 0: wide banner (1000x200), 1: standard (800x1200), 2: middle target (800x1600), 3: tall (800x2400)
$sizes = @(
    @{ W = 1000; H = 200 },
    @{ W = 800;  H = 1200 },
    @{ W = 800;  H = 1600 },
    @{ W = 800;  H = 2400 }
)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bmp = New-Object System.Drawing.Bitmap $sizes[$i].W, $sizes[$i].H
    $p = Join-Path $testMixedDir ("item_{0:D2}.bmp" -f $i)
    $bmp.Save($p)
    $bmp.Dispose()
}

$middleTarget = Join-Path $testMixedDir "item_02.bmp"
$f.LoadSingleImage($middleTarget)

$currIdx = $f.GetType().GetField("_currentIndex", $bind).GetValue($f)
$scrollY = $f.GetType().GetField("_webtoonScrollY", $bind).GetValue($f)
$maxScroll = $f.GetType().GetMethod("GetWebtoonMaxScroll", $bind).Invoke($f, @())
Write-Host "  Loaded middle item -> Index: $currIdx, ScrollY: $scrollY, MaxScroll: $maxScroll"

# Clean up test files
Remove-Item $testMixedDir -Recurse -Force

if ($currIdx -eq 2 -and $scrollY -gt 1000) {
    Write-Host "  Middle file position accurately calculated with mixed ratios: PASS"
} else {
    Write-Host "  FAILED: Expected Index 2 and ScrollY > 1000, got Index $currIdx, ScrollY $scrollY" -ForegroundColor Red
    exit 1
}

# Test 3: FitMode flexibility in Webtoon mode (not locked, default FitWidth)
Write-Host "`n[Test 3] Webtoon FitMode: Default FitWidth & unlocked cycling"
$f.SetViewMode([NyaaView.ViewLayout]::Webtoon)
if ($settings.FitMode -ne [NyaaView.DisplayFitMode]::FitWidth) {
    Write-Host "  FAILED: Expected default FitMode to be FitWidth on Webtoon entry" -ForegroundColor Red
    exit 1
}
Write-Host "  Default FitMode on Webtoon entry is FitWidth: PASS"

# Now cycle fit modes
$f.CycleFitMode()
$modeAfterCycle = $settings.FitMode
Write-Host "  FitMode after CycleFitMode: $modeAfterCycle"
if ($modeAfterCycle -eq [NyaaView.DisplayFitMode]::FitWidth) {
    Write-Host "  FAILED: CycleFitMode was blocked in Webtoon mode" -ForegroundColor Red
    exit 1
}
Write-Host "  FitMode cycling allowed in Webtoon mode: PASS"

# Test SetFitMode
$f.SetFitMode([NyaaView.DisplayFitMode]::Original)
if ($settings.FitMode -ne [NyaaView.DisplayFitMode]::Original) {
    Write-Host "  FAILED: SetFitMode(Original) was blocked in Webtoon mode" -ForegroundColor Red
    exit 1
}
Write-Host "  SetFitMode(Original) allowed in Webtoon mode: PASS"

$f.Dispose()

Write-Host "`n=========================================="
Write-Host "     ALL WEBTOON BUG TESTS PASSED! (3/3)  "
Write-Host "=========================================="
