<#
.SYNOPSIS
  Kills processes listening on the BlazorBootstrapCustomControlsApp dev ports (5175, 7240).
  Run this when you get "port is in use" after rebuilding while the app was running.

.EXAMPLE
  .\scripts\kill-dev-ports.ps1
#>
$ports = @(5175, 7240)
$pids = [System.Collections.Generic.HashSet[int]]::new()

foreach ($port in $ports) {
  $lines = netstat -ano | Select-String ":$port\s.*LISTENING\s+(\d+)\s*$"
  foreach ($line in $lines) {
    if ($line -match 'LISTENING\s+(\d+)\s*$') {
      [void]$pids.Add([int]$Matches[1])
    }
  }
}

if ($pids.Count -eq 0) {
  Write-Host "No processes found listening on ports 5175 or 7240."
  exit 0
}

foreach ($procId in $pids) {
  try {
    $proc = Get-Process -Id $procId -ErrorAction Stop
    Write-Host "Killing PID $procId ($($proc.ProcessName))..."
    Stop-Process -Id $procId -Force
  } catch {
    Write-Warning "Could not kill PID $procId : $_"
  }
}

Write-Host "Done. You can run the app again."
