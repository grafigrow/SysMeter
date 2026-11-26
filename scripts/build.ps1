param(
    [string]$Version = "1.0.0",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$SrcDir = Join-Path $RootDir "src"
$AssetsDir = Join-Path $RootDir "assets"
$DistDir = Join-Path $RootDir "dist"
$PortableStage = Join-Path $DistDir "portable\SysMeter-v$Version-Portable"

Write-Host "==> Building SysMeter v$Version" -ForegroundColor Cyan

$CscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$Csc = $CscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Csc) {
    throw "Could not find csc.exe (.NET Framework 4.x)."
}

New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
$OutExe = Join-Path $DistDir "SysMeter.exe"
$IconPath = Join-Path $AssetsDir "app.ico"
$ManifestPath = Join-Path $SrcDir "app.manifest"
$CsFiles = Get-ChildItem -Path $SrcDir -Filter "*.cs" | ForEach-Object { $_.FullName }

$CscArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/codepage:65001",
    "/utf8output",
    "/r:System.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Web.Extensions.dll",
    "/r:System.Core.dll",
    "/r:System.Data.dll",
    "/win32icon:$IconPath",
    "/win32manifest:$ManifestPath",
    "/out:$OutExe"
) + $CsFiles

Write-Host "==> Compiling with $Csc"
& $Csc @CscArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $OutExe)) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

$ExeInfo = Get-Item $OutExe
Write-Host "    Compiled: $($ExeInfo.FullName) ($($ExeInfo.Length) bytes)" -ForegroundColor Green

# Stage portable zip
if (Test-Path (Join-Path $DistDir "portable")) {
    Remove-Item -Recurse -Force (Join-Path $DistDir "portable")
}
New-Item -ItemType Directory -Force -Path $PortableStage | Out-Null
Copy-Item $OutExe (Join-Path $PortableStage "SysMeter.exe")
Copy-Item $IconPath (Join-Path $PortableStage "app.ico")
Copy-Item (Join-Path $RootDir "LICENSE") (Join-Path $PortableStage "LICENSE")
if (Test-Path (Join-Path $RootDir "README.md")) {
    Copy-Item (Join-Path $RootDir "README.md") (Join-Path $PortableStage "README.md")
}

$PortableZip = Join-Path $DistDir "SysMeter-v$Version-Portable-win.zip"
if (Test-Path $PortableZip) { Remove-Item $PortableZip -Force }
Compress-Archive -Path "$PortableStage\*" -DestinationPath $PortableZip -CompressionLevel Optimal
Write-Host "    Packaged Portable ZIP: $PortableZip" -ForegroundColor Green

# Build Inno Setup installer
if (-not $SkipInstaller) {
    $IsccCandidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    $Iscc = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($Iscc) {
        $IssPath = Join-Path $RootDir "installer\SysMeter.iss"
        Write-Host "==> Building Windows Setup Installer with $Iscc"
        & $Iscc "/DMyAppVersion=$Version" $IssPath
        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
        }
        $SetupExe = Join-Path $DistDir "SysMeter-v$Version-Setup.exe"
        Write-Host "    Built Setup Installer: $SetupExe" -ForegroundColor Green
    } else {
        Write-Warning "ISCC.exe (Inno Setup 6) not found. Skipping Setup.exe build."
    }
}

# Generate SHA256SUMS.txt
$Artifacts = Get-ChildItem -Path $DistDir -File | Where-Object { $_.Name -match '\.(exe|zip)$' } | Sort-Object Name
$SumLines = foreach ($f in $Artifacts) {
    $h = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$h  $($f.Name)"
}
$SumFile = Join-Path $DistDir "SHA256SUMS.txt"
$SumLines | Out-File -FilePath $SumFile -Encoding utf8
Write-Host "==> Checksums written to $SumFile" -ForegroundColor Green
Get-Content $SumFile
