[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$InstallDirectory, [string]$BackupDirectory)

$ErrorActionPreference = 'Stop'
$installPath = (Resolve-Path -LiteralPath $InstallDirectory).Path
$executable = Join-Path $installPath 'RustDeskHop.exe'
$icon = Join-Path $installPath 'Assets\RustDeskHop.ico'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Missing $executable" }
if (-not (Test-Path -LiteralPath $icon -PathType Leaf)) { throw "Missing generated icon: $icon" }

# Windows caches icons by path. This is a derived cache entry, not another master.
$hash = (Get-FileHash -LiteralPath $icon -Algorithm SHA256).Hash.Substring(0, 16)
$cachedIcon = Join-Path $installPath "Assets\RustDeskHop-$hash.ico"
if ($PSCmdlet.ShouldProcess($cachedIcon, 'Refresh generated icon cache entry')) {
    Copy-Item -LiteralPath $icon -Destination $cachedIcon -Force
}

$shortcutPaths = @(
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\RustDeskHop.lnk'),
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\RustDeskHop.lnk'),
    (Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\RustDeskHop.lnk')
)
$shell = New-Object -ComObject WScript.Shell
for ($index = 0; $index -lt $shortcutPaths.Count; $index++) {
    $path = $shortcutPaths[$index]
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $shortcut = $shell.CreateShortcut($path)
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($shortcut.TargetPath, $executable)) { continue }
    if ($PSCmdlet.ShouldProcess($path, 'Use icon generated from the application master')) {
        if ($BackupDirectory) { Copy-Item -LiteralPath $path -Destination (Join-Path $BackupDirectory "shortcut-$index.lnk") }
        $shortcut.IconLocation = "$cachedIcon,0"
        $shortcut.Save()
        Write-Output "Updated RustDeskHop icon: $path"
    }
}
