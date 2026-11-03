param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$Profile
)
$ErrorActionPreference='Stop'
$profilePath=[IO.Path]::GetFullPath($Profile)
if(Test-Path -LiteralPath $profilePath){throw 'Use a new isolated profile.'}
New-Item -ItemType Directory -Path $profilePath | Out-Null
@{version=1;minimizeToTray=$false;closeToTray=$false;restoreLastPage=$true;lastPage='Paramètres';enabledModules=@{pc=$false;network=$false;media=$false;codex=$false;projects=$false}} |
 ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $profilePath 'settings.json') -Encoding utf8
function Read-AxisStartup {
 $startupKey=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
 try{if($startupKey){return $startupKey.GetValue('SHINO.AXIS')}}finally{if($startupKey){$startupKey.Dispose()}}
}
$before=Read-AxisStartup
$arguments=@('--data-dir',('"'+$profilePath+'"'))
$executablePath=[IO.Path]::GetFullPath($Executable)
$first=Start-Process -FilePath $executablePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$second=$null
try {
 Start-Sleep -Seconds 2
 $first.Refresh();if($first.HasExited){throw 'Primary instance exited during startup.'}
 $second=Start-Process -FilePath $executablePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
 if(!$second.WaitForExit(5000)){throw 'Duplicate instance did not exit.'}
 if($second.ExitCode -ne 0){throw 'Duplicate instance failed.'}
 $first.Refresh();if($first.HasExited){throw 'Activation closed primary instance.'}
 $clock=[Diagnostics.Stopwatch]::StartNew()
 if(!$first.CloseMainWindow() -or !$first.WaitForExit(5000)){throw 'Primary did not shut down cleanly.'}
 if($first.ExitCode -ne 0){throw 'Primary exited with an error.'}
 if((Read-AxisStartup) -cne $before){throw 'Real startup registration changed.'}
 [pscustomobject]@{ProfileIsolated=$true;AllCollectorsDisabled=$true;SecondInstanceExit=$second.ExitCode;PrimaryExit=$first.ExitCode;CloseMs=$clock.ElapsedMilliseconds;RealStartupUnchanged=$true;OwnedProcessRemaining=$false} | ConvertTo-Json
} finally {
 # Bounded cleanup applies only to the exact child Process objects created above.
 foreach($child in @($second,$first)){if($child){if(!$child.HasExited){$child.Kill($true);$child.WaitForExit()};$child.Dispose()}}
}
