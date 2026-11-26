# SPDX-License-Identifier: AGPL-3.0-only
param(
    [Parameter(Mandatory = $true)][string]$GameDirectory,
    [switch]$ReplaceExisting
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$exe = Join-Path $game 'Beat Saber.exe'
$plugins = Join-Path $game 'Plugins'
$target = Join-Path $plugins 'PlayerBackup.dll'
$marker = Join-Path $game 'BeatSaberVersion.txt'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $game 'Beat Saber_Data/Managed/IPA.Loader.dll') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $plugins 'BSML.dll') -PathType Leaf)) {
    throw 'Select a Beat Saber 1.40.8 folder with BSIPA and BSML already installed.'
}
if (Get-Process -Name 'Beat Saber' -ErrorAction SilentlyContinue) {
    throw 'Close Beat Saber before installing.'
}
if ((Test-Path -LiteralPath $target) -and -not $ReplaceExisting) {
    throw 'Player Backup already exists. Pass -ReplaceExisting to upgrade.'
}
if ((Test-Path -LiteralPath $marker) -and (Get-Content -Raw -LiteralPath $marker).Trim() -ne '1.40.8') {
    throw 'This installer targets 1.40.8.'
}
$runtime = Join-Path $env:LOCALAPPDATA 'Bsmac/player-sync'
$venv = Join-Path $runtime 'venv'
py -3.12 -m venv $venv
if ($LASTEXITCODE -ne 0) { throw 'Python 3.12 could not create the worker environment.' }
$python = Join-Path $venv 'Scripts/python.exe'
& $python -m pip install $repo
if ($LASTEXITCODE -ne 0) { throw 'Could not install the backup worker.' }
$stage = Join-Path $runtime ('stage-' + [Guid]::NewGuid().ToString('N'))
dotnet build (Join-Path $repo 'implementation/tools/PlayerBackup') -c Release --disable-build-servers "-p:GameResources=$game" -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Could not build the mod.' }
if (Get-Process -Name 'Beat Saber' -ErrorAction SilentlyContinue) { throw 'Beat Saber reopened during installation.' }
$source = Join-Path $stage 'PlayerBackup.dll'
$staged = Join-Path $plugins ('.PlayerBackup-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    Copy-Item -LiteralPath $source -Destination $staged
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $staged).Hash) {
        throw 'The staged mod failed verification.'
    }
    if (Test-Path -LiteralPath $target) {
        $rollback = Join-Path $plugins ('.PlayerBackup-' + [Guid]::NewGuid().ToString('N') + '.bak')
        [IO.File]::Replace($staged, $target, $rollback)
        Write-Output "Previous mod saved at $rollback"
    } else {
        [IO.File]::Move($staged, $target)
    }
    if (-not (Test-Path -LiteralPath $marker)) {
        [IO.File]::WriteAllText($marker, '1.40.8')
    }
    Write-Output "Installed $target"
} finally {
    if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged }
}
