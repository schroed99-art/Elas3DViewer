<#
    Baut die Testfälle und führt sie aus.

    Zusätzliche Argumente werden als echte Modelle mitgeprüft, etwa:
        .\run-tests.ps1 "C:\Pfad\grosses-modell.stl"
#>
param([string[]]$Modelle)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$out = Join-Path $env:TEMP 'elas3dview-tests'
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out | Out-Null }
$exe = Join-Path $out 'SelfTest.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$assemblies = @('System', 'Microsoft.VisualBasic', 'PresentationFramework', 'PresentationCore',
    'WindowsBase', 'System.Xaml', 'System.Xml', 'System.Core', 'System.IO.Compression',
    'System.IO.Compression.FileSystem', 'System.Drawing', 'System.Windows.Forms')
$refs = @()
foreach ($a in $assemblies) { $refs += ('/r:' + [System.Reflection.Assembly]::LoadWithPartialName($a).Location) }

$sources = @((Join-Path $root 'src\MeshLoader.cs'), (Join-Path $root 'tests\SelfTest.cs'))
$cscArgs = @('/nologo', '/noconfig', '/target:exe', '/platform:anycpu', '/codepage:65001',
    ('/out:' + $exe)) + $refs + $sources

$o = & $csc $cscArgs 2>&1
if ($LASTEXITCODE -ne 0) { $o | ForEach-Object { Write-Host $_ }; exit 1 }

$runArgs = @($out)
if ($Modelle) { $runArgs += $Modelle }
& $exe $runArgs
exit $LASTEXITCODE
