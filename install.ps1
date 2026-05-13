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
    Write-Step "Installation du SDK .NET 8 dans le profil utilisateur"
    $dotnetDir = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    $installerPath = Join-Path $tempRoot "dotnet-install.ps1"
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installerPath
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installerPath -Channel 8.0 -InstallDir $dotnetDir
    $env:PATH = "$dotnetDir;$env:PATH"
}

Write-Step "Preparation"
if (Test-Path $tempRoot) {
    Remove-Item $tempRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $tempRoot | Out-Null

if (-not (Test-DotNetSdk)) {
    Install-DotNetSdk
}

Write-Step "Telechargement du repo $repoOwner/$repoName"
Invoke-WebRequest $repoZipUrl -OutFile $zipPath
Expand-Archive $zipPath -DestinationPath $tempRoot -Force

Write-Step "Publication de l'application"
dotnet publish (Join-Path $sourceDir "RobloxInstanceOptimizer.App\RobloxInstanceOptimizer.App.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

Write-Step "Installation dans $installDir"
if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}
New-Item -ItemType Directory -Path $installDir | Out-Null
Copy-Item (Join-Path $publishDir "*") $installDir -Recurse -Force

$exePath = Join-Path $installDir "RobloxInstanceOptimizer.App.exe"
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Roblox Instance Optimizer.lnk"

Write-Step "Creation du raccourci bureau"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "Roblox Instance Optimizer"
$shortcut.Save()

Write-Step "Nettoyage"
Remove-Item $tempRoot -Recurse -Force

Write-Host ""
Write-Host "Installation terminee." -ForegroundColor Green
Write-Host "Lance le raccourci 'Roblox Instance Optimizer' en administrateur depuis le bureau."
Write-Host "Exe installe: $exePath"
