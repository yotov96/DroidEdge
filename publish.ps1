# Builds a single self-contained executable into .\dist
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet test tests/DroidEdge.Tests --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "tests failed" }
dotnet publish src/DroidEdge -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
Write-Host "Published to $PSScriptRoot\dist\DroidEdge.exe"
