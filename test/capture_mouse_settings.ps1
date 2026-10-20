[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$settings = [NyaaView.Settings]::Load()
$form = New-Object NyaaView.SettingsForm($settings)
$form.SwitchTab(1)
$form.Show()
[System.Windows.Forms.Application]::DoEvents()
Start-Sleep -Milliseconds 50

$bmp = New-Object System.Drawing.Bitmap($form.Width, $form.Height)
$form.DrawToBitmap($bmp, (New-Object System.Drawing.Rectangle(0, 0, $form.Width, $form.Height)))

$outPath = "$PSScriptRoot\settings_mouse_autohide.png"
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$form.Close()
$form.Dispose()
Write-Host "Captured settings mouse tab to $outPath"
