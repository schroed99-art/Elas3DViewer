#requires -version 3
<#
    Baut Elas3DView.exe mit dem C#-Compiler, der bereits Teil von Windows ist
    (.NET Framework 4.x). Es muss nichts installiert werden.
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$srcDir = Join-Path $root 'src'
$binDir = Join-Path $root 'bin'
$exe = Join-Path $binDir 'Elas3DView.exe'
$ico = Join-Path $binDir 'Elas3DView.ico'

if (-not (Test-Path $binDir)) { New-Item -ItemType Directory -Path $binDir | Out-Null }

# ---------------------------------------------------------------- Compiler
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "C#-Compiler nicht gefunden. Ist .NET Framework 4.x installiert?" }

# Reste frueherer Baeue aufraeumen, sobald sie nicht mehr benutzt werden.
Get-ChildItem -Path $binDir -Filter 'Elas3DView.alt-*.exe' -ErrorAction SilentlyContinue | ForEach-Object {
    try { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction Stop } catch { }
}

# Eine laufende exe laesst sich nicht ueberschreiben — wohl aber umbenennen.
# Die laufende Instanz arbeitet unbeirrt weiter, der Platz wird frei.
if (Test-Path $exe) {
    $running = Get-Process -Name 'Elas3DView' -ErrorAction SilentlyContinue
    if ($running) {
        $parked = Join-Path $binDir ('Elas3DView.alt-' + (Get-Date -Format 'HHmmss') + '.exe')
        try {
            Rename-Item -LiteralPath $exe -NewName (Split-Path $parked -Leaf) -ErrorAction Stop
            Write-Host 'Hinweis: Die Anwendung laeuft. Der neue Stand wird daneben gelegt;' -ForegroundColor Yellow
            Write-Host '         die offene Instanz zeigt ihn erst nach einem Neustart.' -ForegroundColor Yellow
        }
        catch {
            throw "Elas3DView.exe laeuft und liess sich nicht beiseitelegen. Bitte schliessen und erneut bauen."
        }
    }
}

# ---------------------------------------------------------------- Icon
Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0

    # Abgerundeter Hintergrund mit Verlauf
    $r = 46.0 * $s
    $w = $size - 1
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($w - 2 * $r, 0, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($w - 2 * $r, $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc(0, $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 34, 29, 41),
        [System.Drawing.Color]::FromArgb(255, 21, 18, 26), 90.0)
    $g.FillPath($bg, $path)

    # Isometrischer Wuerfel
    $cx = $size / 2.0
    $cy = $size / 2.0 + 2 * $s
    $R = 82.0 * $s
    $ang = @(330, 30, 90, 150, 210, 270)
    $v = @()
    foreach ($a in $ang) {
        $rad = $a * [Math]::PI / 180.0
        $v += , (New-Object System.Drawing.PointF(
            [float]($cx + $R * [Math]::Cos($rad)),
            [float]($cy + $R * [Math]::Sin($rad))))
    }
    $c = New-Object System.Drawing.PointF([float]$cx, [float]$cy)

    $top = @($v[5], $v[0], $c, $v[4])
    $right = @($v[0], $v[1], $v[2], $c)
    $left = @($c, $v[2], $v[3], $v[4])

    $bTop = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 214, 166))
    $bRight = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 211, 154, 57))
    $bLeft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 138, 101, 36))
    $g.FillPolygon($bTop, [System.Drawing.PointF[]]$top)
    $g.FillPolygon($bRight, [System.Drawing.PointF[]]$right)
    $g.FillPolygon($bLeft, [System.Drawing.PointF[]]$left)

    if ($size -ge 32) {
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 21, 18, 26), [float](3.0 * $s))
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPolygon($pen, [System.Drawing.PointF[]]$top)
        $g.DrawPolygon($pen, [System.Drawing.PointF[]]$right)
        $g.DrawPolygon($pen, [System.Drawing.PointF[]]$left)
        $pen.Dispose()
    }

    $g.Dispose()
    return $bmp
}

function Write-Ico([string]$path) {
    $sizes = @(256, 64, 48, 32, 16)
    $blobs = @()
    foreach ($sz in $sizes) {
        $bmp = New-IconBitmap $sz
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $blobs += , $ms.ToArray()
        $ms.Dispose()
        $bmp.Dispose()
    }

    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([UInt16]0)                       # reserviert
    $bw.Write([UInt16]1)                       # Typ 1 = Icon
    $bw.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz = $sizes[$i]
        $dim = 0
        if ($sz -lt 256) { $dim = $sz }
        $bw.Write([byte]$dim)                  # Breite (0 = 256)
        $bw.Write([byte]$dim)                  # Hoehe
        $bw.Write([byte]0)                     # Farbanzahl
        $bw.Write([byte]0)                     # reserviert
        $bw.Write([UInt16]1)                   # Ebenen
        $bw.Write([UInt16]32)                  # Bit pro Pixel
        $bw.Write([UInt32]$blobs[$i].Length)
        $bw.Write([UInt32]$offset)
        $offset += $blobs[$i].Length
    }
    foreach ($b in $blobs) { $bw.Write($b) }
    $bw.Flush(); $bw.Close(); $fs.Dispose()
}

Write-Host 'Erzeuge Symbol ...' -ForegroundColor DarkGray
Write-Ico $ico

# ---------------------------------------------------------------- Kompilieren
$assemblies = @(
    'System',
    'Microsoft.VisualBasic',
    'PresentationFramework',
    'PresentationCore',
    'WindowsBase',
    'System.Xaml',
    'System.Xml',
    'System.Core',
    'System.IO.Compression',
    'System.IO.Compression.FileSystem',
    'System.Drawing',
    'System.Windows.Forms',
    'System.Net.Http',
    'System.Web.Extensions',
    'System.Security'
)
$refs = @()
foreach ($a in $assemblies) {
    $loaded = [System.Reflection.Assembly]::LoadWithPartialName($a)
    if ($loaded -eq $null) { throw "Assembly nicht gefunden: $a" }
    $refs += ('/r:' + $loaded.Location)
}

$sources = Get-ChildItem -Path $srcDir -Filter *.cs | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "Keine Quelldateien in $srcDir gefunden." }

# Die mitgelieferten Logos wandern als Ressource in die exe. Beim ersten Start
# schreibt die Anwendung sie nach bin\logos, wo sie austauschbar sind.
$assetDir = Join-Path $root 'assets'
$resources = @()
if (Test-Path $assetDir) {
    Get-ChildItem -Path $assetDir -File | ForEach-Object {
        $resources += ('/resource:' + $_.FullName + ',' + $_.Name)
    }
    # Darunter auch die Bildmarke des Kopfbands — sie ist kein Wasserzeichen und
    # wird nicht nach bin\logos geschrieben (welche das sind, steht in Logo.cs).
    Write-Host ('Eingebettet: ' + $resources.Count + ' Bild(er)') -ForegroundColor DarkGray
}

Write-Host 'Kompiliere ...' -ForegroundColor DarkGray
$cscArgs = @(
    '/nologo',
    '/noconfig',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/warn:2',
    ('/win32icon:' + $ico),
    ('/out:' + $exe)
) + $resources + $refs + $sources

$output = & $csc $cscArgs 2>&1
$code = $LASTEXITCODE
$output | Where-Object { $_ -notmatch 'only supports language versions' -and $_ -notmatch '^\s*$' -and $_ -notmatch 'Copyright \(C\) Microsoft' -and $_ -notmatch 'Visual C# Compiler version' -and $_ -notmatch 'for C# 5' -and $_ -notmatch 'This compiler is provided' } | ForEach-Object { Write-Host $_ }

if ($code -ne 0 -or -not (Test-Path $exe)) {
    Write-Host ''
    Write-Host 'FEHLGESCHLAGEN.' -ForegroundColor Red
    exit 1
}

$size = [Math]::Round((Get-Item $exe).Length / 1KB, 1)
Write-Host ''
Write-Host ('Fertig: ' + $exe + '  (' + $size + ' kB)') -ForegroundColor Green
