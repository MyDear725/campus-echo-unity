param([string[]]$Only = @())
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$player = Join-Path $projectRoot 'Builds\Windows\CampusEcho.exe'
$outputDirectory = Join-Path $projectRoot 'Builds\Windows\QA\FinalAudit'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$results = @()
$checks = @(
    @{ Flag = '-finalEnvironmentQA'; Result = 'FinalAudit\routes-result.txt' },
    @{ Flag = '-campusPlanQA'; Result = 'CampusPlan\result.txt' },
    @{ Flag = '-teachingABQA'; Result = 'TeachingABInterior\result.txt' },
    @{ Flag = '-teachingCQA'; Result = 'TeachingCInterior\result.txt' },
    @{ Flag = '-gymInteriorQA'; Result = 'GymnasiumInterior\result.txt' },
    @{ Flag = '-interiorRefinementQA'; Result = 'InteriorRefinement\result.txt' },
    @{ Flag = '-entranceQA'; Result = 'Interiors\result.txt' },
    @{ Flag = '-courtMapQA'; Result = 'CourtMap\result.txt'; Visible = $true },
    @{ Flag = '-storyStyleQA'; Result = 'StoryPresentation\result.txt'; Visible = $true },
    @{ Flag = '-vroidPresentationQA'; Result = 'VRoid\result.txt' }
)
if ($Only.Count -gt 0) {
    $checks = @($checks | Where-Object { $Only -contains $_.Flag.TrimStart('-') })
    $previousResults = Join-Path $outputDirectory 'suite-results.json'
    if (Test-Path -LiteralPath $previousResults) {
        $results = @((Get-Content -LiteralPath $previousResults -Raw -Encoding utf8 | ConvertFrom-Json) | Where-Object { $Only -notcontains $_.Check })
    }
}
foreach ($check in $checks) {
    $name = $check.Flag.TrimStart('-')
    $logPath = Join-Path $outputDirectory ($name + '.log')
    $arguments = @($check.Flag, '-screen-width', '1440', '-screen-height', '900', '-screen-fullscreen', '0', '-logFile', $logPath)
    $startOptions = @{ FilePath = $player; ArgumentList = $arguments; WorkingDirectory = $projectRoot; PassThru = $true }
    if (-not $check.Visible) { $startOptions.WindowStyle = 'Hidden' }
    Write-Output "START $name"
    $run = Start-Process @startOptions
    $timeoutAt = (Get-Date).AddSeconds(360)
    while (-not $run.WaitForExit(10000)) {
        if ((Get-Date) -gt $timeoutAt) { $run.Kill(); throw "Timeout: $name" }
    }
    $resultPath = Join-Path (Join-Path $projectRoot 'Builds\Windows\QA') $check.Result
    $result = if (Test-Path -LiteralPath $resultPath) { [string](Get-Content -LiteralPath $resultPath -TotalCount 1 -Encoding utf8) } else { 'Missing result' }
    $results += [pscustomobject]@{ Check = $name; ExitCode = $run.ExitCode; Result = $result }
    $results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDirectory 'suite-results.json') -Encoding utf8
    Write-Output "$name : exit=$($run.ExitCode) : $result"
}
$failures = @($results | Where-Object { $_.ExitCode -ne 0 -or [string]$_.Result -notlike 'PASS:*' })
if ($failures.Count -gt 0) { throw "Failed checks: $(($failures.Check) -join ', ')" }
