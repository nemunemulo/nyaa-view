[System.Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\nyaa-view.exe") | Out-Null
$z = New-Object NyaaView.ZipEngine
$entries = $z.LoadZip("$PSScriptRoot\Downloads.zip")
for ($i = 0; $i -lt $entries.Count; $i++) {
    Write-Host "$i : $($entries[$i].Name)"
}
$z.Close()
