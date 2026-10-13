# Builds scrcpy's Windows client with droidedge.patch into .\dist\scrcpy-desktop, together with the DLLs it needs.
# DroidEdge runs this client for the phone desktop window when it exists.
# Needs a scrcpy v4.1 checkout with the patch applied (see build-server.ps1) and MSYS2 with scrcpy's MinGW64 build
# packages (scrcpy's doc/build.md, "In MSYS2"):
#   pacman -S mingw-w64-x86_64-sdl3 mingw-w64-x86_64-ffmpeg mingw-w64-x86_64-libusb mingw-w64-x86_64-make
#             mingw-w64-x86_64-gcc mingw-w64-x86_64-pkg-config mingw-w64-x86_64-meson mingw-w64-x86_64-ninja
param(
    [string]$ScrcpySource = 'C:\tools\scrcpy',
    [string]$Msys = 'C:\tools\msys64',
    [string]$Output = (Join-Path $PSScriptRoot '..\dist\scrcpy-desktop')
)
$ErrorActionPreference = 'Stop'
$env:MSYSTEM = 'MINGW64'
$env:CHERE_INVOKING = '1'
$bash = Join-Path $Msys 'usr\bin\bash.exe'

function Invoke-Msys([string]$command) {
    & $bash -lc $command
    if ($LASTEXITCODE -ne 0) { throw "MSYS2 command failed with exit code ${LASTEXITCODE}: $command" }
}

$source = (& (Join-Path $Msys 'usr\bin\cygpath.exe') -u $ScrcpySource).Trim()

# Client only: the server is built by build-server.ps1. --strip and LTO match scrcpy's release build.
Invoke-Msys "cd '$source' && rm -rf build-win && meson setup build-win --buildtype=release --strip -Db_lto=true -Dcompile_server=false -Dportable=true && ninja -C build-win"

# Copy the DLLs that come from MSYS2's MinGW64 tree, as a fixed snapshot: a later MSYS2 update can then not break this
# client. ldd also lists Windows' own DLLs and ones that security software injects into every process; those stay out.
[string[]]$dlls = Invoke-Msys "ldd '$source/build-win/app/scrcpy.exe' | awk '`$3 ~ /^\/mingw64\// {print `$3}' | sort -u | xargs -r cygpath -w"

Remove-Item $Output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $Output | Out-Null
Copy-Item (Join-Path $ScrcpySource 'build-win\app\scrcpy.exe') $Output
# A portable build loads its window icon and "disconnected" image from next to the executable
Copy-Item (Join-Path $ScrcpySource 'app\data\scrcpy.png'), (Join-Path $ScrcpySource 'app\data\disconnected.png') $Output
foreach ($dll in $dlls) { Copy-Item $dll.Trim() $Output }
Write-Host "Client generated in $((Resolve-Path $Output).Path) with $($dlls.Count) DLLs"
