Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

Write-Host "=========================================="
Write-Host "  nyaa-view Webtoon Smooth Scroll Tests   "
Write-Host "=========================================="

# 1. Settings test
Write-Host "`n[Test 1] WebtoonSmoothScroll Setting Persistence"
$s = [NyaaView.Settings]::Load()
if ($s.WebtoonSmoothScroll -ne $true) {
    Write-Host "FAILED: Default WebtoonSmoothScroll is not true!" -ForegroundColor Red
    exit 1
}
Write-Host "  Default WebtoonSmoothScroll is TRUE: PASS"

$s.WebtoonSmoothScroll = $false
$s.Save()
$reloaded = [NyaaView.Settings]::Load()
if ($reloaded.WebtoonSmoothScroll -ne $false) {
    Write-Host "FAILED: WebtoonSmoothScroll did not persist false!" -ForegroundColor Red
    exit 1
}
Write-Host "  Persist WebtoonSmoothScroll = FALSE: PASS"

# Re-enable
$reloaded.WebtoonSmoothScroll = $true
$reloaded.Save()
Write-Host "  Restored WebtoonSmoothScroll = TRUE: PASS"

# 2. MainForm Webtoon Smooth Scroll Integration
Write-Host "`n[Test 2] MainForm Smooth Scroll Timer & Ease-out Deceleration"
$m = New-Object NyaaView.MainForm
$zipPath = "$PSScriptRoot\..\sample_test\album_A.zip"
$m.LoadArchive($zipPath, 0)
$m.SetViewMode([NyaaView.ViewLayout]::Webtoon)

$bind = [System.Reflection.BindingFlags]"NonPublic,Instance"
$timerField = $m.GetType().GetField("_smoothScrollTimer", $bind)
$targetField = $m.GetType().GetField("_webtoonTargetScrollY", $bind)
$scrollField = $m.GetType().GetField("_webtoonScrollY", $bind)
$smoothScrollByMethod = $m.GetType().GetMethod("SmoothScrollWebtoonBy", $bind)
$tickMethod = $m.GetType().GetMethod("SmoothScrollTimer_Tick", $bind)
$scrollToPageMethod = $m.GetType().GetMethod("ScrollToPageIndex", $bind)

$timer = $timerField.GetValue($m)
$initialY = $scrollField.GetValue($m)
$initialTarget = $targetField.GetValue($m)

Write-Host "  Initial ScrollY: $initialY, TargetScrollY: $initialTarget"
if ($initialY -ne 0 -or $initialTarget -ne 0) {
    Write-Host "FAILED: Initial webtoon scroll is not 0" -ForegroundColor Red
    exit 1
}

# Simulate 1 notch wheel down (220px)
Write-Host "  Simulating mouse wheel roll by 220px..."
$smoothScrollByMethod.Invoke($m, @([float]220.0))

$targetAfterWheel = $targetField.GetValue($m)
$timerEnabled = $timer.Enabled
Write-Host "  TargetScrollY after wheel: $targetAfterWheel (Expected: 220)"
Write-Host "  SmoothScrollTimer running: $timerEnabled (Expected: True)"

if ($targetAfterWheel -ne 220.0 -or -not $timerEnabled) {
    Write-Host "FAILED: SmoothScrollWebtoonBy did not set target or start timer!" -ForegroundColor Red
    exit 1
}

# Tick 1
$tickMethod.Invoke($m, @($null, [EventArgs]::Empty))
$scrollY_tick1 = $scrollField.GetValue($m)
Write-Host "  ScrollY after Tick 1: $scrollY_tick1 (Expected: > 0 and < 220)"
if ($scrollY_tick1 -le 0 -or $scrollY_tick1 -ge 220.0) {
    Write-Host "FAILED: Tick 1 ease-out step invalid!" -ForegroundColor Red
    exit 1
}

# Advance until settled
$ticks = 1
while ($timer.Enabled -and $ticks -lt 50) {
    $tickMethod.Invoke($m, @($null, [EventArgs]::Empty))
    $ticks++
}
$finalScrollY = $scrollField.GetValue($m)
Write-Host "  Settled in $ticks ticks: Final ScrollY: $finalScrollY, Timer running: $($timer.Enabled)"
if ($finalScrollY -ne 220.0 -or $timer.Enabled) {
    Write-Host "FAILED: Animation did not settle cleanly at 220.0!" -ForegroundColor Red
    exit 1
}
Write-Host "  Smooth Deceleration Curve Settled Cleanly: PASS"

# 3. Test Rapid Wheel Accumulation
Write-Host "`n[Test 3] Rapid Multi-notch Wheel Accumulation"
$smoothScrollByMethod.Invoke($m, @([float]220.0))
$smoothScrollByMethod.Invoke($m, @([float]220.0))
$targetAfterMulti = $targetField.GetValue($m)
Write-Host "  TargetScrollY after two rapid wheels: $targetAfterMulti (Expected: 660)"
if ($targetAfterMulti -ne 660.0) {
    Write-Host "FAILED: Rapid wheel events did not accumulate target!" -ForegroundColor Red
    exit 1
}
Write-Host "  Multi-notch Accumulation: PASS"

# 4. Test Immediate Jump (ScrollToPageIndex cancels animation)
Write-Host "`n[Test 4] Direct Page Jump Cancels Animation"
$scrollToPageMethod.Invoke($m, @([int]0))
$scrollAfterJump = $scrollField.GetValue($m)
$targetAfterJump = $targetField.GetValue($m)
$timerAfterJump = $timer.Enabled
Write-Host "  After Jump -> ScrollY: $scrollAfterJump, TargetScrollY: $targetAfterJump, Timer: $timerAfterJump"
if ($scrollAfterJump -ne 0 -or $targetAfterJump -ne 0 -or $timerAfterJump) {
    Write-Host "FAILED: Page jump did not immediately stop smooth scroll!" -ForegroundColor Red
    exit 1
}
Write-Host "  Direct Page Jump Cancel: PASS"

# 5. Boundary Condition Test
Write-Host "`n[Test 5] Boundary Counter on Repeated Edge Scrolls"
$counterField = $m.GetType().GetField("_webtoonBoundaryCounter", $bind)
# Scroll up when already at 0
$smoothScrollByMethod.Invoke($m, @([float]-220.0))
$counter1 = $counterField.GetValue($m)
$smoothScrollByMethod.Invoke($m, @([float]-220.0))
$counter2 = $counterField.GetValue($m)
Write-Host "  Boundary counter on top edge wheel: $counter1 -> $counter2"
if ($counter1 -ne 1 -or $counter2 -ne 2) {
    Write-Host "FAILED: Boundary counter not incrementing on edge scrolls!" -ForegroundColor Red
    exit 1
}
Write-Host "  Boundary Detection: PASS"

$m.Close()
$m.Dispose()

Write-Host "`n=========================================="
Write-Host "  ALL SMOOTH SCROLL TESTS PASSED! (5/5)   "
Write-Host "=========================================="
