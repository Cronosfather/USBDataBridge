param([ValidateSet('Connect','Restore','Watch','Diagnose')][string]$Mode='Restore',[int]$ParentId=0,[int]$TunnelId=0)
$ErrorActionPreference='Stop'
$aliasName='USBDataBridge2'
function OwnAdapter {
 $a=Get-NetAdapter -Name $aliasName -IncludeHidden -ErrorAction SilentlyContinue
 if($a) {
  if(-not ('USBDataBridgeAdapterCheck' -as [type])) {Add-Type -Path (Join-Path $PSScriptRoot 'AdapterCheck.dll')}
  if(-not [USBDataBridgeAdapterCheck]::IsWintun($PSScriptRoot,$aliasName)) {
   throw ('USBDataBridge2 어댑터를 Wintun으로 확인하지 못했습니다. 설명: '+$a.InterfaceDescription)
  }
 }
 return $a
}
function Restore {
 $a=OwnAdapter
 if(!$a){return}
 $idx=$a.ifIndex
 Get-NetRoute -InterfaceIndex $idx -ErrorAction SilentlyContinue | Where-Object {$_.DestinationPrefix -in @('0.0.0.0/1','128.0.0.0/1','::/1','8000::/1')} | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue
 Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses -ErrorAction SilentlyContinue
 Get-NetIPAddress -InterfaceIndex $idx -ErrorAction SilentlyContinue | Where-Object {$_.IPAddress -in @('198.18.42.1','fd42:42:42::1')} | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue
}
if($Mode -eq 'Diagnose') {
 $a=Get-NetAdapter -Name $aliasName -IncludeHidden -ErrorAction SilentlyContinue
 if($a){
  $a | Select-Object Name,Status,InterfaceDescription,ifIndex | Format-List | Out-String -Width 180 | Write-Output
  Get-NetIPAddress -InterfaceIndex $a.ifIndex -ErrorAction SilentlyContinue | Select-Object IPAddress,AddressFamily,AddressState | Format-Table | Out-String -Width 180 | Write-Output
  Get-NetRoute -InterfaceIndex $a.ifIndex -ErrorAction SilentlyContinue | Select-Object DestinationPrefix,NextHop,RouteMetric | Format-Table | Out-String -Width 180 | Write-Output
  Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ErrorAction SilentlyContinue | Select-Object AddressFamily,ServerAddresses | Format-Table | Out-String -Width 180 | Write-Output
 }else{Write-Output 'USBDataBridge2 어댑터 없음'}
 foreach($ip in @('1.1.1.1','8.8.8.8')) {
  Write-Output ('선택된 Windows 경로: '+$ip)
  try{Find-NetRoute -RemoteIPAddress $ip | Select-Object InterfaceAlias,InterfaceIndex,DestinationPrefix,NextHop,IPAddress | Format-List | Out-String -Width 180 | Write-Output}catch{Write-Output $_.Exception.Message}
 }
 exit
}
if($Mode -eq 'Watch') {
 try {$parent=Get-Process -Id $ParentId -ErrorAction Stop;$parent.WaitForExit()} catch {}
 Restore
 # Only stop the child whose executable belongs to this distribution.
 if($TunnelId -gt 0){$t=Get-Process -Id $TunnelId -ErrorAction SilentlyContinue;if($t -and $t.Path -eq (Join-Path $PSScriptRoot 'tun2socks.exe')){Stop-Process -Id $TunnelId -ErrorAction SilentlyContinue}}
 exit
}
if($Mode -eq 'Restore'){Restore;exit}
try {
 $a=$null
 for($i=0;$i -lt 100;$i++){$a=OwnAdapter;if($a){break};Start-Sleep -Milliseconds 200}
 if(!$a){throw '가상 어댑터가 만들어지지 않았습니다.'}
 Restore
 $idx=$a.ifIndex
 Set-NetIPInterface -InterfaceIndex $idx -AddressFamily IPv4 -Dhcp Disabled -InterfaceMetric 5 -NlMtuBytes 1400
 Set-NetIPInterface -InterfaceIndex $idx -AddressFamily IPv6 -Dhcp Disabled -InterfaceMetric 5 -NlMtuBytes 1400
 New-NetIPAddress -InterfaceIndex $idx -IPAddress '198.18.42.1' -PrefixLength 30 -AddressFamily IPv4 | Out-Null
 New-NetIPAddress -InterfaceIndex $idx -IPAddress 'fd42:42:42::1' -PrefixLength 64 -AddressFamily IPv6 | Out-Null
 Set-DnsClientServerAddress -InterfaceIndex $idx -ServerAddresses @('1.1.1.1','1.0.0.1')
 foreach($prefix in @('0.0.0.0/1','128.0.0.0/1')) {New-NetRoute -InterfaceIndex $idx -DestinationPrefix $prefix -NextHop '0.0.0.0' -RouteMetric 1 -PolicyStore ActiveStore | Out-Null}
 foreach($prefix in @('::/1','8000::/1')) {New-NetRoute -InterfaceIndex $idx -DestinationPrefix $prefix -NextHop '::' -RouteMetric 1 -PolicyStore ActiveStore | Out-Null}
 # USB forwarding is loopback. Its 127/8 route remains more specific than these /1 routes.
 Write-Output 'CONNECTED'
} catch {Restore;throw}
