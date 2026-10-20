Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

$bind = [System.Reflection.BindingFlags]"NonPublic,Instance"

Write-Host "=========================================="
Write-Host "   nyaa-view Page Transition Tests        "
Write-Host "=========================================="

# 1. Settings Default & Enum Verification
Write-Host "`n[Test 1] Settings Default PageTransition (Must be None / 0)"
$s = New-Object NyaaView.Settings
Write-Host "  Default PageTransition: $($s.PageTransition)"
if ($s.PageTransition -ne [NyaaView.PageTransitionEffect]::None) {
    Write-Host "FAILED: Expected default PageTransition None, got $($s.PageTransition)" -ForegroundColor Red
    exit 1
}
Write-Host "  Default is OFF (None): PASS"

# 2. Persistence / Serialization of PageTransition
Write-Host "`n[Test 2] PageTransition Serialization and Deserialization"
$savedSettings = [NyaaView.Settings]::Load()
$origVal = $savedSettings.PageTransition
try {
    $savedSettings.PageTransition = [NyaaView.PageTransitionEffect]::Fade
    $savedSettings.Save()
    $reloaded = [NyaaView.Settings]::Load()
    if ($reloaded.PageTransition -ne [NyaaView.PageTransitionEffect]::Fade) {
        Write-Host "FAILED: Expected Fade after reload, got $($reloaded.PageTransition)" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Fade persistence: PASS"

    $savedSettings.PageTransition = [NyaaView.PageTransitionEffect]::Slide
    $savedSettings.Save()
    $reloaded = [NyaaView.Settings]::Load()
    if ($reloaded.PageTransition -ne [NyaaView.PageTransitionEffect]::Slide) {
        Write-Host "FAILED: Expected Slide after reload, got $($reloaded.PageTransition)" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Slide persistence: PASS"

    $savedSettings.PageTransition = [NyaaView.PageTransitionEffect]::PageTurn
    $savedSettings.Save()
    $reloaded = [NyaaView.Settings]::Load()
    if ($reloaded.PageTransition -ne [NyaaView.PageTransitionEffect]::PageTurn) {
        Write-Host "FAILED: Expected PageTurn after reload, got $($reloaded.PageTransition)" -ForegroundColor Red
        exit 1
    }
    Write-Host "  PageTurn persistence: PASS"
}
finally {
    $savedSettings.PageTransition = $origVal
    $savedSettings.Save()
}

# 3. SettingsForm UI ComboBox Verification
Write-Host "`n[Test 3] SettingsForm ComboBox Items"
$formSettings = New-Object NyaaView.Settings
$form = New-Object NyaaView.SettingsForm($formSettings)
$comboField = $form.GetType().GetField("_comboPageTransition", $bind)
$combo = $comboField.GetValue($form)

if ($combo -eq $null) {
    Write-Host "FAILED: _comboPageTransition field not found on SettingsForm" -ForegroundColor Red
    exit 1
}
if ($combo.Items.Count -ne 4) {
    Write-Host "FAILED: Expected 4 options in _comboPageTransition, got $($combo.Items.Count)" -ForegroundColor Red
    exit 1
}
Write-Host "  Option 0: $($combo.Items[0])"
Write-Host "  Option 1: $($combo.Items[1])"
Write-Host "  Option 2: $($combo.Items[2])"
Write-Host "  Option 3: $($combo.Items[3])"
if ($combo.SelectedIndex -ne 0) {
    Write-Host "FAILED: Expected SelectedIndex 0 (Off by default), got $($combo.SelectedIndex)" -ForegroundColor Red
    exit 1
}
$form.Dispose()
Write-Host "  SettingsForm UI ComboBox: PASS"

# 4. MainForm Transition Engine Functional Check
Write-Host "`n[Test 4] MainForm Transition Execution"
$main = New-Object NyaaView.MainForm
$main.CreateControl()
$handle = $main.Handle
$main.ClientSize = New-Object System.Drawing.Size(800, 600)

$mainSettingsField = $main.GetType().GetField("_settings", $bind)
$mainSettings = $mainSettingsField.GetValue($main)
$mainSettings.PageTransition = [NyaaView.PageTransitionEffect]::Slide
$mainSettings.ViewMode = [NyaaView.ViewLayout]::Single

$sampleZip = "$PSScriptRoot\..\sample_test\album_A.zip"
$main.LoadArchive($sampleZip, 0)

$isTransField = $main.GetType().GetField("_isTransitioning", $bind)
$snapField = $main.GetType().GetField("_transitionSnapshot", $bind)

# Before navigation: not transitioning
$isTrans = $isTransField.GetValue($main)
if ($isTrans) {
    Write-Host "FAILED: Should not be transitioning immediately after LoadArchive" -ForegroundColor Red
    exit 1
}

# Invoke NextImage via reflection
$nextMethod = $main.GetType().GetMethod("NextImage", $bind)
$nextMethod.Invoke($main, $null)

$isTrans = $isTransField.GetValue($main)
$snap = $snapField.GetValue($main)

Write-Host "  After NextImage(): isTransitioning = $isTrans, snapshot = $(if ($snap -ne $null) { 'Present' } else { 'Null' })"
if (-not $isTrans) {
    Write-Host "FAILED: Expected _isTransitioning to be true after NextImage with Slide effect" -ForegroundColor Red
    exit 1
}
if ($snap -eq $null) {
    Write-Host "FAILED: Expected _transitionSnapshot to be captured" -ForegroundColor Red
    exit 1
}

# Invoke EndTransition via reflection
$endMethod = $main.GetType().GetMethod("EndTransition", $bind)
$endMethod.Invoke($main, $null)

$isTransAfter = $isTransField.GetValue($main)
$snapAfter = $snapField.GetValue($main)
Write-Host "  After EndTransition(): isTransitioning = $isTransAfter, snapshot = $(if ($snapAfter -ne $null) { 'Present' } else { 'Null' })"

if ($isTransAfter) {
    Write-Host "FAILED: Expected _isTransitioning to be false after EndTransition" -ForegroundColor Red
    exit 1
}
if ($snapAfter -ne $null) {
    Write-Host "FAILED: Expected _transitionSnapshot to be null after EndTransition" -ForegroundColor Red
    exit 1
}

# Test Webtoon mode bypass (Transitions must never fire in Webtoon mode)
$mainSettings.PageTransition = [NyaaView.PageTransitionEffect]::Slide
$main.SetViewMode([NyaaView.ViewLayout]::Webtoon)
$nextMethod.Invoke($main, $null)
$isTransWebtoon = $isTransField.GetValue($main)
if ($isTransWebtoon) {
    Write-Host "FAILED: Transitions must NOT trigger in Webtoon mode" -ForegroundColor Red
    exit 1
}
Write-Host "  Webtoon mode transition bypass: PASS"

$main.Dispose()
Write-Host "  MainForm Transition Engine: PASS"

Write-Host "`n=========================================="
Write-Host " ALL TRANSITION TESTS PASSED SUCCESSFULLY! "
Write-Host "=========================================="
