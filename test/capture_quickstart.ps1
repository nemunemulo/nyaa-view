Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null

$s = New-Object NyaaView.Settings
$s.FirstRunTutorialShown = $false
$s.WheelMode = 1
$s.CacheLimitMb = 1024

$form = New-Object NyaaView.QuickStartForm($s)
$form.Show()
[System.Windows.Forms.Application]::DoEvents()
Start-Sleep -Milliseconds 300

$bmp = New-Object System.Drawing.Bitmap $form.Width, $form.Height
$rect = New-Object System.Drawing.Rectangle 0, 0, $form.Width, $form.Height
$form.DrawToBitmap($bmp, $rect)

$outPath = "$PSScriptRoot\quickstart_preview.png"
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$form.Close()
$form.Dispose()

Write-Host "Saved preview to: $outPath"
