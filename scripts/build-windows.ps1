param([string]$OutputDirectory=(Join-Path $PSScriptRoot '../dist'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw '.NET Framework x64 C# compiler was not found.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$source=Join-Path $root 'src/windows'
$files=@('Launcher.cs','SocksBridge.cs','UsageCheck.cs','Diagnostics.cs','DnsOverHttps.cs','UdpAssociation.cs','UiTheme.cs') | ForEach-Object {Join-Path $source $_}
& $compiler /nologo /target:winexe /platform:x64 ("/win32manifest:"+(Join-Path $source 'admin.manifest')) ("/out:"+(Join-Path $OutputDirectory 'USBDataBridge.exe')) /r:System.Windows.Forms.dll /r:System.Drawing.dll $files
if($LASTEXITCODE -ne 0){throw 'Launcher compilation failed.'}
& $compiler /nologo /target:winexe /platform:x64 ("/win32manifest:"+(Join-Path $source 'admin.manifest')) ("/out:"+(Join-Path $OutputDirectory '인터넷경로복구.exe')) /r:System.Windows.Forms.dll (Join-Path $source 'Recovery.cs')
if($LASTEXITCODE -ne 0){throw 'Recovery compilation failed.'}
& $compiler /nologo /target:library /platform:x64 ("/out:"+(Join-Path $OutputDirectory 'AdapterCheck.dll')) (Join-Path $source 'AdapterCheck.cs')
if($LASTEXITCODE -ne 0){throw 'Adapter helper compilation failed.'}
Copy-Item -LiteralPath (Join-Path $root 'runtime/network.ps1') -Destination $OutputDirectory -Force
Write-Output 'Build complete. Add USBDataBridge.apk, tun2socks.exe, wintun.dll and platform-tools with their licenses before packaging.'
