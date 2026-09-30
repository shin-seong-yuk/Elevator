$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testExe = Join-Path $projectRoot 'Builds/Windows/Elevator.exe'
$launched = @()
function Wait-Report([string]$path, [string]$pattern, [int]$seconds = 90) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        if (Test-Path -LiteralPath $path) {
            $text = [IO.File]::ReadAllText($path)
            if ($text -match 'FAIL') { throw $text }
            if ($text -match $pattern) { return }
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $pattern in $path"
}
function Start-Node([int]$node) {
    $log = Join-Path $PSScriptRoot "migration-node-$node.log"
    $arguments = "-batchmode -nographics -job-worker-count 2 -migrationNode $node -logFile `"$log`""
    Start-Process -FilePath $testExe -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
}
try {
    foreach ($name in @('migration-new-host.ready', 'migration-transfer.bin', 'migration-node-0.txt', 'migration-node-1.txt', 'migration-node-2.txt')) {
        $path = Join-Path $PSScriptRoot $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    $hostNode = Start-Node 0; $launched += $hostNode
    Wait-Report (Join-Path $PSScriptRoot 'migration-node-0.txt') 'CONNECTED'
    $launched += Start-Node 1
    Wait-Report (Join-Path $PSScriptRoot 'migration-node-1.txt') 'CONNECTED'
    $launched += Start-Node 2
    Wait-Report (Join-Path $PSScriptRoot 'migration-node-0.txt') 'READY_TO_KILL'
    # Only the host process started above is terminated to simulate a crash.
    Stop-Process -InputObject $hostNode -Force
    Write-Output 'Original host terminated; waiting for checkpoint reconstruction.'
    Wait-Report (Join-Path $PSScriptRoot 'migration-node-1.txt') 'COMPLETE'
    Wait-Report (Join-Path $PSScriptRoot 'migration-node-2.txt') 'COMPLETE'
    Get-Content (Join-Path $PSScriptRoot 'migration-node-1.txt')
    Get-Content (Join-Path $PSScriptRoot 'migration-node-2.txt')
    foreach ($process in $launched) {
        if (-not $process.WaitForExit(15000)) { throw "Test process $($process.Id) did not exit after completion" }
    }
    foreach ($node in 0..2) {
        $log = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "migration-node-$node.log"))
        if ($log -match 'Serialization depth|NullReferenceException|MissingReferenceException|MissingComponentException|InvalidOperationException') {
            throw "Runtime or serialization failure in node $node"
        }
    }
    Write-Output 'PASS All three process logs free of runtime exceptions and serialization depth warnings.'
} finally {
    foreach ($process in $launched) {
        $process.Refresh()
        if (-not $process.HasExited) { Stop-Process -InputObject $process -Force }
    }
}
