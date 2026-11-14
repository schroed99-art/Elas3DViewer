#requires -version 3
<#
    Baut dist\Elas3DView-Setup.exe: eine einzelne Datei, in der die Anwendung
    steckt. Wie build.ps1 nur mit dem C#-Compiler, der zu Windows gehoert.

    Zuerst wird die Anwendung frisch gebaut, damit das Setup nie einen alten
    Stand aus bin\ verpackt.
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$binDir = Join-Path $root 'bin'
$distDir = Join-Path $root 'dist'
$exe = Join-Path $binDir 'Elas3DView.exe'
$ico = Join-Path $binDir 'Elas3DView.ico'
$out = Join-Path $distDir 'Elas3DView-Setup.exe'

# ---------------------------------------------------------------- Anwendung
& (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw 'Die Anwendung liess sich nicht bauen.' }

# ---------------------------------------------------------------- Stand
# Der Stand ist der letzte Commit. Weicht der Quelltext davon ab, zeigt das
# ein '+' - ein Setup soll nicht behaupten, es sei ein Commit, der es nicht ist.
$datum = Get-Date
$commit = 'ohne Git'
try {
    $d = (& git -C $root log -1 --format=%cd --date=format:%Y-%m-%d 2>$null)
    $h = (& git -C $root rev-parse --short HEAD 2>$null)
    if ($d -and $h) {
        $datum = [datetime]::ParseExact($d.Trim(), 'yyyy-MM-dd', $null)
        $commit = $h.Trim()
        $offen = (& git -C $root status --porcelain -- src assets 2>$null)
        if ($offen) { $commit += '+' }
    }
} catch { }
$anzeige = $datum.ToString('yyyy.MM.dd') + ' (' + $commit + ')'
$version = $datum.Year.ToString() + '.' + $datum.Month + '.' + $datum.Day + '.0'

$gen = Join-Path $env:TEMP ('Elas3DSetup-Stand-' + [guid]::NewGuid().ToString('N') + '.cs')
@"
using System.Reflection;
[assembly: AssemblyTitle("Elas3DView Setup")]
[assembly: AssemblyProduct("Elas3DView")]
[assembly: AssemblyCompany("Elas3DStudio")]
[assembly: AssemblyVersion("$version")]
[assembly: AssemblyFileVersion("$version")]
namespace Elas3DSetup
{
    internal static class Stand
    {
        public const string Datum = "$($datum.ToString('dd.MM.yyyy'))";
        public const string Anzeige = "$anzeige";
    }
}
"@ | Set-Content -LiteralPath $gen -Encoding ASCII

# ---------------------------------------------------------------- Kompilieren
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }

$refs = @()
foreach ($a in @('System', 'System.Drawing', 'System.Windows.Forms')) {
    $refs += ('/r:' + [System.Reflection.Assembly]::LoadWithPartialName($a).Location)
}

if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }

Write-Host 'Baue Setup ...' -ForegroundColor DarkGray
$cscArgs = @(
    '/nologo',
    '/noconfig',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/warn:2',
    ('/win32icon:' + $ico),
    ('/win32manifest:' + (Join-Path $root 'setup\setup.manifest')),
    ('/resource:' + $exe + ',payload/Elas3DView.exe'),
    ('/resource:' + $ico + ',payload/Elas3DView.ico'),
    ('/out:' + $out)
) + $refs + @((Join-Path $root 'setup\Setup.cs'), $gen)

try {
    $output = & $csc $cscArgs 2>&1
    $code = $LASTEXITCODE
} finally {
    Remove-Item -LiteralPath $gen -ErrorAction SilentlyContinue
}
$output | Where-Object { $_ -notmatch 'only supports language versions' -and $_ -notmatch '^\s*$' -and $_ -notmatch 'Copyright \(C\) Microsoft' -and $_ -notmatch 'Visual C# Compiler version' -and $_ -notmatch 'for C# 5' -and $_ -notmatch 'This compiler is provided' } | ForEach-Object { Write-Host $_ }

if ($code -ne 0 -or -not (Test-Path $out)) {
    Write-Host ''
    Write-Host 'FEHLGESCHLAGEN.' -ForegroundColor Red
    exit 1
}

$size = [Math]::Round((Get-Item $out).Length / 1KB, 1)
Write-Host ''
Write-Host ('Fertig: ' + $out + '  (' + $size + ' kB, Stand ' + $anzeige + ')') -ForegroundColor Green
