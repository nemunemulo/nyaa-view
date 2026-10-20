Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

$settings = [NyaaView.Settings]::Load()
$settings.AutoHideUi = $true
$settings.Save()

$m = New-Object NyaaView.MainForm
# Load the GIF at index 2
$m.LoadArchive("$PSScriptRoot\Downloads.zip", 2)

$topBar = $m.GetType().GetField("_topBar", [System.Reflection.BindingFlags]"NonPublic,Instance").GetValue($m)
$gifBar = $m.GetType().GetField("_gifBar", [System.Reflection.BindingFlags]"NonPublic,Instance").GetValue($m)
$statusStrip = $m.GetType().GetField("_statusStrip", [System.Reflection.BindingFlags]"NonPublic,Instance").GetValue($m)
$frameLbl = $m.GetType().GetField("_gifFrameLbl", [System.Reflection.BindingFlags]"NonPublic,Instance").GetValue($m)
$statusLbl = $m.GetType().GetField("_statusLabel", [System.Reflection.BindingFlags]"NonPublic,Instance").GetValue($m)

Write-Host "GIF loaded! Frame label:" $frameLbl.Text
Write-Host "Status label:" $statusLbl.Text
Write-Host "Initial TopBar Visible:" $topBar.Visible
Write-Host "Initial GifBar Visible:" $gifBar.Visible
Write-Host "Initial StatusStrip Visible:" $statusStrip.Visible

$m.Show()
[System.Windows.Forms.Application]::DoEvents()
Start-Sleep -Milliseconds 100

# 1. Clean View (Both Top and Bottom hidden, full screen GIF)
$bmp = New-Object System.Drawing.Bitmap($m.Width, $m.Height)
$m.DrawToBitmap($bmp, (New-Object System.Drawing.Rectangle(0, 0, $m.Width, $m.Height)))
$bmp.Save("$PSScriptRoot\gif_autohide_clean.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved gif_autohide_clean.png"

# 2. Bottom Hover (status bar and GIF bar with timeline and hide button appear)
$statusStrip.Visible = $true
$gifBar.Visible = $true
$m.Refresh()
$bmp = New-Object System.Drawing.Bitmap($m.Width, $m.Height)
$m.DrawToBitmap($bmp, (New-Object System.Drawing.Rectangle(0, 0, $m.Width, $m.Height)))
$bmp.Save("$PSScriptRoot\gif_autohide_bottom_hover.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved gif_autohide_bottom_hover.png"

# 3. Click Hide button
$hideBtn = $m.GetType().GetField("_gifHideBtn", [System.Reflection.BindingFlags]"NonPublic,Instance").GetValue($m)
$hideBtn.PerformClick()
$m.Refresh()
$bmp = New-Object System.Drawing.Bitmap($m.Width, $m.Height)
$m.DrawToBitmap($bmp, (New-Object System.Drawing.Rectangle(0, 0, $m.Width, $m.Height)))
$bmp.Save("$PSScriptRoot\gif_autohide_after_hide.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved gif_autohide_after_hide.png (GifBar: $($gifBar.Visible))"

$m.Close()
$m.Dispose()
