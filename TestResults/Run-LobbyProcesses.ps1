$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root 'Builds/Windows/Elevator.exe'
$owned=[System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function Wait-Report($path,$pattern,$seconds=100){$end=(Get-Date).AddSeconds($seconds);do{if((Test-Path $path)-and([IO.File]::ReadAllText($path)-match $pattern)){return};Start-Sleep -Milliseconds 500}while((Get-Date)-lt$end);throw "Timed out: $pattern in $path"}
try {
 foreach($role in @('host','client')) {
  $report=Join-Path $PSScriptRoot "network-weapons-$role.txt";$log=Join-Path $PSScriptRoot "network-weapons-$role.log"
  [IO.File]::WriteAllText($report,'')
  $arguments="-batchmode -nographics -job-worker-count 2 -elevator$role -elevatorReport `"$report`" -logFile `"$log`""
  # Command-line flags are case-sensitive in the game's argument parser.
  $arguments=$arguments.Replace('-elevatorhost','-elevatorHost').Replace('-elevatorclient','-elevatorClient')
  $process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -PassThru
  $owned.Add($process)
  if($role-eq'host'){Wait-Report $report 'phase=Lobby'}
 }
 foreach($role in @('host','client')){Wait-Report (Join-Path $PSScriptRoot "network-weapons-$role.txt") 'COMPLETE'}
 foreach($process in $owned){if(-not $process.WaitForExit(15000)){throw 'Game did not exit'}}
 $hostText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'network-weapons-host.txt'))
 $clientText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'network-weapons-client.txt'))
 foreach($marker in @('PASS HOST BLOCKED BEFORE READY','PASS CANCEL REPLICATED','PASS HOST R START')){if(-not $hostText.Contains($marker)){throw "Missing $marker"};Write-Output $marker}
 if(-not $clientText.Contains('READY ACTION 3')){throw 'Client did not toggle ready/cancel/ready'}
 Write-Output 'PASS client ready/cancel/ready RPC sequence'
 & (Join-Path $PSScriptRoot 'validate-weapon-network.ps1')
} finally { foreach($process in $owned){if(-not $process.HasExited){Stop-Process -Id $process.Id -Force}} }
