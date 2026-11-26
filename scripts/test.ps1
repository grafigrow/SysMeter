$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$SrcDir = Join-Path $RootDir "src"
$DistDir = Join-Path $RootDir "dist"
$ExePath = Join-Path $DistDir "SysMeter.exe"

Write-Host "=== SysMeter Verification & Compatibility Suite ===" -ForegroundColor Cyan

# 1. Verify build artifacts exist
if (-not (Test-Path $ExePath)) {
    throw "Missing compiled binary: $ExePath. Run scripts\build.ps1 first."
}

$VerInfo = (Get-Item $ExePath).VersionInfo
Write-Host "[1/5] Binary metadata: Product=$($VerInfo.ProductName) FileVersion=$($VerInfo.FileVersion)"
if ($VerInfo.FileVersion -ne "1.0.0.0") {
    throw "Unexpected FileVersion: $($VerInfo.FileVersion)"
}

# 2. Compile and run C# unit tests against core logic
Write-Host "[2/5] Running C# unit tests..."
$TestHarness = Join-Path $env:TEMP "SysMeterUnitTests.exe"
$TestCs = Join-Path $env:TEMP "SysMeterUnitTests.cs"

@'
using System;
using System.Collections.Generic;
using System.Threading;
using SysMeter;

class UnitTests
{
    static void Assert(bool cond, string msg)
    {
        if (!cond) throw new Exception("ASSERT FAILED: " + msg);
    }

    static void Main()
    {
        // ClampPercent tests
        Assert(SystemMonitor.ClampPercent(-10) == 0, "negative clamped");
        Assert(SystemMonitor.ClampPercent(double.NaN) == 0, "NaN clamped");
        Assert(SystemMonitor.ClampPercent(double.PositiveInfinity) == 0, "Infinity clamped");
        Assert(SystemMonitor.ClampPercent(150) == 100, "over 100 clamped");
        Assert(Math.Abs(SystemMonitor.ClampPercent(42.5) - 42.5) < 0.001, "42.5 preserved");

        // FormatGb test
        Assert(SystemMonitor.FormatGb(0) == "0 GB", "0 GB");
        Assert(SystemMonitor.FormatGb(16L * 1024L * 1024L * 1024L) == "16.0 GB", "16.0 GB");

        // AppSettings validation tests
        AppSettings s = new AppSettings();
        s.WidgetOpacity = 0.0;
        s.AutoClosePopupSeconds = -5;
        s.ShowCpu = false;
        s.ShowRam = false;
        s.ShowDisk = false;
        s.Normalize();
        Assert(Math.Abs(s.WidgetOpacity - 0.95) < 0.001, "Invalid opacity normalized");
        Assert(s.AutoClosePopupSeconds == 0, "Negative autoclose normalized");
        Assert(s.ShowCpu == true, "At least one metric forced on");

        // DailySystemRecord average tests
        DailySystemRecord rec = new DailySystemRecord
        {
            DateString = "2026-11-26",
            PeakCpuPercent = 80,
            SumCpuPercent = 150,
            SumRamPercent = 200,
            SumDiskPercent = 30,
            SampleCount = 5
        };
        Assert(Math.Abs(rec.AvgCpuPercent - 30.0) < 0.001, "AvgCpuPercent");
        Assert(Math.Abs(rec.AvgRamPercent - 40.0) < 0.001, "AvgRamPercent");
        Assert(Math.Abs(rec.AvgDiskPercent - 6.0) < 0.001, "AvgDiskPercent");

        // Live SystemMonitor poll test
        AppSettings liveSet = new AppSettings();
        SystemMonitor mon = new SystemMonitor(liveSet);
        Thread.Sleep(250);
        mon.PollOnce();
        Assert(mon.TotalRamBytes > 0, "TotalRamBytes > 0");
        Assert(mon.CurrentRamPercent > 0 && mon.CurrentRamPercent <= 100, "Valid RAM %");
        Assert(mon.CurrentCpuPercent >= 0 && mon.CurrentCpuPercent <= 100, "Valid CPU %");
        Assert(mon.CurrentDiskPercent >= 0 && mon.CurrentDiskPercent <= 100, "Valid Disk %");
        Console.WriteLine("Live poll: CPU={0:0.0}% RAM={1:0.0}% ({2} / {3}) Disk={4:0.0}%",
            mon.CurrentCpuPercent, mon.CurrentRamPercent,
            SystemMonitor.FormatGb(mon.UsedRamBytes), SystemMonitor.FormatGb(mon.TotalRamBytes),
            mon.CurrentDiskPercent);
        mon.Stop();

        // DataManager merge test
        DailySystemRecord live = new DailySystemRecord { DateString = DateTime.Now.ToString("yyyy-MM-dd"), PeakCpuPercent = 77.7, SampleCount = 10 };
        List<DailySystemRecord> merged = DataManager.GetMergedHistory(live);
        Assert(merged.Exists(x => x.DateString == live.DateString && Math.Abs(x.PeakCpuPercent - 77.7) < 0.01), "Merged history includes live today record");

        Console.WriteLine("ALL UNIT TESTS PASSED");
    }
}
'@ | Out-File -FilePath $TestCs -Encoding utf8

$Csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$CoreCs = Get-ChildItem -Path $SrcDir -Filter "*.cs" | Where-Object { $_.Name -notin @("Program.cs", "AssemblyInfo.cs") } | ForEach-Object { $_.FullName }
& $Csc /nologo /target:exe /utf8output /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Core.dll /r:System.Data.dll /out:$TestHarness $TestCs @CoreCs
if ($LASTEXITCODE -ne 0) { throw "Unit test compile failed" }
& $TestHarness
if ($LASTEXITCODE -ne 0) { throw "Unit test execution failed" }
Remove-Item $TestHarness, $TestCs -Force -ErrorAction SilentlyContinue

# 3. Verify Portable ZIP contents
Write-Host "[3/5] Checking Portable ZIP package..."
$ZipFile = Get-ChildItem -Path $DistDir -Filter "*Portable*.zip" | Select-Object -First 1
if (-not $ZipFile) { throw "Portable ZIP not found in $DistDir" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Zip = [System.IO.Compression.ZipFile]::OpenRead($ZipFile.FullName)
$EntryNames = $Zip.Entries | ForEach-Object { $_.FullName }
$Zip.Dispose()
foreach ($req in @("SysMeter.exe", "app.ico", "LICENSE", "README.md")) {
    if ($EntryNames -notcontains $req) { throw "Portable ZIP missing $req" }
}
Write-Host "    Portable ZIP verified ($($EntryNames -join ', '))" -ForegroundColor Green

# 4. Verify Setup Installer exists
Write-Host "[4/5] Checking Windows Setup Installer..."
$SetupFile = Get-ChildItem -Path $DistDir -Filter "*Setup.exe" | Select-Object -First 1
if (-not $SetupFile) { throw "Setup installer not found in $DistDir" }
Write-Host "    Setup Installer verified: $($SetupFile.Name) ($($SetupFile.Length) bytes)" -ForegroundColor Green

# 5. Live Launch + Single Instance Mutex Check
Write-Host "[5/5] Testing live launch & single-instance mutex..."
Get-Process -Name SysMeter -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600

$p1 = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 2
$running = Get-Process -Name SysMeter -ErrorAction SilentlyContinue
if (-not $running) { throw "SysMeter failed to stay running after launch!" }

# Launch second instance - should exit immediately due to mutex
$p2 = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 1
$count = (Get-Process -Name SysMeter -ErrorAction SilentlyContinue | Measure-Object).Count
if ($count -ne 1) {
    throw "Single-instance mutex failed! Expected 1 process, found $count."
}
Write-Host "    Single-instance mutex verified (1 process running, PID $($running[0].Id))." -ForegroundColor Green

# Clean up test instance
Stop-Process -Name SysMeter -Force -ErrorAction SilentlyContinue
Write-Host "=== ALL VERIFICATION TESTS PASSED ===" -ForegroundColor Green
