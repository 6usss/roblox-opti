$ErrorActionPreference = "Stop"

$repoOwner = "6usss"
$repoName = "roblox-opti"
$branch = "main"
$appName = "RobloxInstanceOptimizer"
$installDir = Join-Path $env:LOCALAPPDATA $appName
$tempRoot = Join-Path $env:TEMP "$appName-install"
$zipPath = Join-Path $tempRoot "$repoName.zip"
$sourceDir = Join-Path $tempRoot "$repoName-$branch"
$publishDir = Join-Path $tempRoot "publish"
$repoZipUrl = "https://github.com/$repoOwner/$repoName/archive/refs/heads/$branch.zip"

function Write-Step($message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Test-DotNetSdk {
    try {
        $sdkVersion = & dotnet --version 2>$null
        return -not [string]::IsNullOrWhiteSpace($sdkVersion)
    }
    catch {
        return $false
    }
}

function Install-DotNetSdk {
    Write-Step "Installing .NET 8 SDK in the user profile"
    $dotnetDir = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    $installerPath = Join-Path $tempRoot "dotnet-install.ps1"
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installerPath
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installerPath -Channel 8.0 -InstallDir $dotnetDir
    $env:PATH = "$dotnetDir;$env:PATH"
}

Write-Step "Preparing"
if (Test-Path $tempRoot) {
    Remove-Item $tempRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $tempRoot | Out-Null

if (-not (Test-DotNetSdk)) {
    Install-DotNetSdk
}

Write-Step "Downloading repo $repoOwner/$repoName"
Invoke-WebRequest $repoZipUrl -OutFile $zipPath
Expand-Archive $zipPath -DestinationPath $tempRoot -Force

Write-Step "Publishing the app"
dotnet publish (Join-Path $sourceDir "RobloxInstanceOptimizer.App\RobloxInstanceOptimizer.App.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

Write-Step "Installing to $installDir"
if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}
New-Item -ItemType Directory -Path $installDir | Out-Null
Copy-Item (Join-Path $publishDir "*") $installDir -Recurse -Force

$exePath = Join-Path $installDir "RobloxInstanceOptimizer.App.exe"
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Roblox Instance Optimizer.lnk"

Write-Step "Creating desktop shortcut"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "Roblox Instance Optimizer"
$shortcut.Save()

Write-Step "Cleaning up"
Remove-Item $tempRoot -Recurse -Force

Write-Host ""
Write-Host "Installation complete." -ForegroundColor Green
Write-Host "Run the 'Roblox Instance Optimizer' desktop shortcut as administrator."
Write-Host "Installed exe: $exePath"
