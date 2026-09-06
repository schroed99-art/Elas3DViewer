$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exe = Join-Path $root 'bin\Elas3DView.exe'
if (-not (Test-Path $exe)) { throw "bin\Elas3DView.exe fehlt. Bitte zuerst build.cmd ausfuehren." }

$shell = New-Object -ComObject WScript.Shell
$targets = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Elas3DView.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'Elas3DView.lnk')
)
foreach ($t in $targets) {
    $lnk = $shell.CreateShortcut($t)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = (Split-Path $exe)
    $lnk.IconLocation = $exe + ',0'
    $lnk.Description = 'STL- und 3MF-Modelle ansehen, Vorschaubilder erzeugen, umbenennen und aufraeumen'
    $lnk.Save()
    Write-Host ('Verknuepfung angelegt: ' + $t) -ForegroundColor Green
}
