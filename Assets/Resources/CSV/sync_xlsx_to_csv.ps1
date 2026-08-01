$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = @("Items.xlsx", "Equipment.xlsx", "EquipmentAffixes.xlsx", "CraftingRecipes.xlsx", "Consumables.xlsx", "LootTables.xlsx", "Entities.xlsx", "Quests.xlsx")

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

foreach ($file in $files) {
    $xlsxPath = Join-Path $scriptDir $file
    $csvPath = [System.IO.Path]::ChangeExtension($xlsxPath, ".csv")

    if (-not (Test-Path $xlsxPath)) {
        Write-Output ("SKIP: " + $file)
        continue
    }

    if (Test-Path $csvPath) { Remove-Item $csvPath -Force }

    $wb = $excel.Workbooks.Open($xlsxPath)
    $wb.SaveAs($csvPath, 6)
    $wb.Close()

    # Convert ANSI -> UTF-8 and replace ; with ,
    $content = [System.IO.File]::ReadAllText($csvPath, [System.Text.Encoding]::Default)
    $content = $content.Replace(';', ',')
    [System.IO.File]::WriteAllText($csvPath, $content, [System.Text.Encoding]::UTF8)

    Write-Output ("OK: " + $file)
}

$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
Write-Output "ALL DONE"


