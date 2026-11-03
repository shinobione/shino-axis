param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$Profile,
 [int]$Seconds=20
)
$ErrorActionPreference='Stop'
# Dedicated temporary profile only; no user settings or existing process are touched.
$profilePath=[IO.Path]::GetFullPath($Profile)
if(Test-Path -LiteralPath (Join-Path $profilePath 'settings.json')){throw 'Use a new measurement profile.'}
New-Item -ItemType Directory -Force -Path $profilePath | Out-Null
@{version=1;codexRefreshSeconds=30;notifyMissionCompletion=$false;minimizeToTray=$false;enabledModules=@{pc=$true;network=$true;media=$true;codex=$false;projects=$false}} |
 ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $profilePath 'settings.json') -Encoding utf8
$process=Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -ArgumentList @('--data-dir',('"'+$profilePath+'"')) -WindowStyle Hidden -PassThru
try {
 Start-Sleep -Seconds 5
 $process.Refresh();if($process.HasExited){throw 'AXIS exited during warm-up.'}
 $cpuStart=$process.TotalProcessorTime.TotalSeconds;$clock=[Diagnostics.Stopwatch]::StartNew();$memory=@();$private=@()
 for($i=0;$i -lt $Seconds;$i++){
  Start-Sleep -Seconds 1;$process.Refresh()
  if($process.HasExited){throw 'AXIS exited during measurement.'}
  $memory+=$process.WorkingSet64/1MB;$private+=$process.PrivateMemorySize64/1MB
 }
 $elapsed=$clock.Elapsed.TotalSeconds
 [pscustomobject]@{Executable=[IO.Path]::GetFileName((Split-Path $Executable));Seconds=[math]::Round($elapsed,2);CpuOneCorePercent=[math]::Round(100*($process.TotalProcessorTime.TotalSeconds-$cpuStart)/$elapsed,2);WorkingSetMiB=[math]::Round(($memory|Measure-Object -Average).Average,1);PeakWorkingSetMiB=[math]::Round(($memory|Measure-Object -Maximum).Maximum,1);PrivateMiB=[math]::Round(($private|Measure-Object -Average).Average,1);ProfileIsolated=$true;CodexAndProjectsDisabled=$true} | ConvertTo-Json
} finally {
 if(!$process.HasExited){[void]$process.CloseMainWindow();if(!$process.WaitForExit(5000)){$process.Kill($true)}}
 $process.Dispose()
}
