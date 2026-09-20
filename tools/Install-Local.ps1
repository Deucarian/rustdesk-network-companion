[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$InstallDirectory,
    [Parameter(Mandatory)][string]$BackupDirectory
)

$ErrorActionPreference = 'Stop'
$publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path
$installPath = (Resolve-Path -LiteralPath $InstallDirectory).Path
if ([StringComparer]::OrdinalIgnoreCase.Equals($publishPath, $installPath)) { throw 'Publish and install directories must differ.' }
$executable = Join-Path $installPath 'RustDeskHop.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'This updater requires an existing RustDeskHop installation.' }
if (Get-Process -Name RustDeskHop -ErrorAction SilentlyContinue | Where-Object Path -eq $executable) {
    throw 'Close this installation of RustDeskHop before updating. Do not close RustDesk or its sessions.'
}
if (Test-Path -LiteralPath $BackupDirectory) { throw 'Use a fresh backup directory; existing backups are never overwritten.' }
$files = @('RustDeskHop.exe', 'Assets\RustDeskHop.ico', 'Assets\RustDeskHop.png', 'LICENSE', 'README.md')
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishPath $file) -PathType Leaf)) { throw "Incomplete publish: $file" }
}
New-Item -ItemType Directory -Path $BackupDirectory | Out-Null
foreach ($file in $files) {
    $installed = Join-Path $installPath $file
    $backup = Join-Path $BackupDirectory $file
    New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
    if (Test-Path -LiteralPath $installed -PathType Leaf) { Copy-Item -LiteralPath $installed -Destination $backup }
}
foreach ($file in $files) {
    $destination = Join-Path $installPath $file
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $publishPath $file) -Destination $destination -Force
}
& (Join-Path $PSScriptRoot 'Update-LocalBranding.ps1') -InstallDirectory $installPath -BackupDirectory $BackupDirectory
Write-Output "Updated $executable. Previous files and matching shortcuts are backed up in $BackupDirectory."
