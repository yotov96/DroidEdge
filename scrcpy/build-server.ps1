# Builds scrcpy's server with droidedge.patch into .\dist\scrcpy-server-desktop.
# Same steps as scrcpy's server/build_without_gradle.sh, which does not run on Windows (d8 is a .bat, ':' classpath).
# Needs a scrcpy v4.1 checkout with the patch applied, a JDK 17 and the Android SDK packages
# platforms;android-36 and build-tools;36.0.0. To prepare the checkout:
#   git clone --branch v4.1 https://github.com/Genymobile/scrcpy.git C:\tools\scrcpy
#   git -C C:\tools\scrcpy am <this folder>\droidedge.patch
param(
    [string]$ScrcpySource = 'C:\tools\scrcpy',
    [string]$Jdk = (Get-ChildItem C:\tools -Directory -Filter 'jdk-17*' | Select-Object -First 1).FullName,
    [string]$Sdk = 'C:\tools\android-sdk',
    [string]$Output = (Join-Path $PSScriptRoot '..\dist\scrcpy-server-desktop')
)
$ErrorActionPreference = 'Stop'
$env:JAVA_HOME = $Jdk # d8.bat looks for java here

# Native tools are called directly: routing their arguments through a function lets PowerShell parse options such
# as -Xlint:-options as the function's own parameters
function Assert-Success([string]$tool) {
    if ($LASTEXITCODE -ne 0) { throw "$tool failed with exit code $LASTEXITCODE" }
}

$version = '4.1' # scrcpy.exe refuses a server whose version differs from its own
$platform = "$Sdk\platforms\android-36"
$buildTools = "$Sdk\build-tools\36.0.0"
$server = Join-Path $ScrcpySource 'server\src\main'
$work = Join-Path ([IO.Path]::GetTempPath()) 'droidedge-server-build'
$gen = "$work\gen"
$classes = "$work\classes"

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$gen\com\genymobile\scrcpy", $classes | Out-Null
@"
package com.genymobile.scrcpy;

public final class BuildConfig {
  public static final boolean DEBUG = false;
  public static final String VERSION_NAME = "$version";
}
"@ | Set-Content "$gen\com\genymobile\scrcpy\BuildConfig.java" -Encoding utf8

Write-Host 'Generating java from aidl...'
# Absolute paths: on Windows aidl rejects the relative "-I." form that the bash script uses
$aidl = "$server\aidl"
& "$buildTools\aidl.exe" "-o$gen" "-I$aidl" "$aidl\android\content\IOnPrimaryClipChangedListener.aidl"
Assert-Success aidl
& "$buildTools\aidl.exe" "-o$gen" "-I$aidl" -p "$platform\framework.aidl" "$aidl\android\view\IDisplayWindowListener.aidl"
Assert-Success aidl

Write-Host 'Compiling java sources...'
# javac argument files treat '\' as an escape character inside quotes
Get-ChildItem "$server\java" -Recurse -Filter *.java |
    ForEach-Object { '"' + ($_.FullName -replace '\\', '/') + '"' } |
    Set-Content "$work\sources.txt" -Encoding utf8
& "$Jdk\bin\javac.exe" -encoding UTF-8 -Xlint:-options -bootclasspath "$platform\android.jar" `
    -cp "$buildTools\core-lambda-stubs.jar;$gen" -d $classes -source 1.8 -target 1.8 "@$work\sources.txt"
Assert-Success javac

Write-Host 'Dexing...'
# Pass the classes as one jar: listing them individually overflows cmd.exe's command line limit for d8.bat
& "$Jdk\bin\jar.exe" cf "$work\classes.jar" -C $classes .
Assert-Success jar
& "$buildTools\d8.bat" --classpath "$platform\android.jar" --output "$work\classes.zip" "$work\classes.jar"
Assert-Success d8

New-Item -ItemType Directory -Force (Split-Path $Output) | Out-Null
Move-Item "$work\classes.zip" $Output -Force
Remove-Item $work -Recurse -Force
Write-Host "Server generated in $((Resolve-Path $Output).Path)"
