$ErrorActionPreference = 'Stop'

$processNames = @(
    'XerahS',
    'xerahs-watchfolder-daemon'
)

foreach ($processName in $processNames) {
    $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($null -eq $processes) {
        continue
    }

    foreach ($process in @($processes)) {
        Write-Host "Stopping $($process.ProcessName) (PID $($process.Id)) before modifying the package."
        Stop-Process -Id $process.Id -Force -ErrorAction Stop
    }

    Wait-Process -Id (@($processes) | Select-Object -ExpandProperty Id) -Timeout 15 -ErrorAction SilentlyContinue
}
