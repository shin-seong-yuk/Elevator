$hostReport = Get-Content 'TestResults/network-weapons-host.txt' -Raw
$clientReport = Get-Content 'TestResults/network-weapons-client.txt' -Raw
$checks = [System.Collections.Generic.List[string]]::new()
function Verify([bool]$condition,[string]$label) { if(-not $condition) { throw $label }; $checks.Add('PASS '+$label) }
Verify ($hostReport.Contains('COMPLETE') -and $clientReport.Contains('COMPLETE')) 'Both standalone processes completed'
Verify ($clientReport -match 'players=2') 'Client sees both players'
Verify ($hostReport -match 'phase=Playing' -and $clientReport -match 'phase=Playing') 'Host and client enter shared round'
Verify ($hostReport -match 'held=0' -and $clientReport -match 'held=0') 'Weapon holder replicated'
Verify ($hostReport -match 'projectiles=1' -and $clientReport -match 'projectiles=1') 'Projectile replicated'
Verify ($hostReport -match 'feedback=[1-9]\d*' -and $clientReport -match 'feedback=[1-9]\d*') 'Confirmed combat feedback received on client'
Verify ($hostReport -match 'broken=163840' -and $clientReport -match 'broken=163840') 'Floor and wall damage replicated'
Verify ($hostReport -match 'phase=Results.*winner=0' -and $clientReport -match 'phase=Results.*winner=0') 'Winner and results replicated'
Verify ($hostReport -match '(?s)phase=Results.*phase=Playing floor=1 stage=Moving' -and $clientReport -match '(?s)phase=Results.*phase=Playing floor=1 stage=Moving') 'Restart replicated'
Verify ($clientReport -match 'kinematic=True' -and $hostReport -match 'kinematic=False') 'Physics stays host authoritative'
$hostSeed = [regex]::Match($hostReport,'phase=Playing.*?seed=(\d+)').Groups[1].Value
$clientSeed = [regex]::Match($clientReport,'phase=Playing.*?seed=(\d+)').Groups[1].Value
Verify ($hostSeed -eq $clientSeed -and $hostSeed -ne '') 'Round seed shared'
$logs = (Get-Content 'TestResults/network-weapons-host.log' -Raw)+(Get-Content 'TestResults/network-weapons-client.log' -Raw)
Verify ($logs -notmatch 'NullReferenceException|MissingComponentException|MissingReferenceException|error CS\d|RpcException') 'No runtime reference or RPC exceptions'
$checks | Set-Content 'TestResults/network-weapons-validation.txt'
$checks
