param (
    [Parameter(Position = 0)] [ValidateSet("Debug", "Release")]        [string]$Configuration = "Release",
    [Parameter(Position = 1)] [ValidateSet("tcmatch", "tcmatch.Core")] [string]$Project       = "tcmatch.Core"
)

$Interactive = $PSBoundParameters.Count -eq 0

$ErrorActionPreference = "Stop"

trap {
    Write-Host ""
    Write-Host "ERROR:" -ForegroundColor Red
    Write-Host $_ -ForegroundColor Red
    Write-Host ""

    if ($Interactive) {
        Read-Host "Press Enter to exit"
    }

    exit 1
}

Write-Host ""
Write-Host "=== QSX2 Build & Deploy ===" -ForegroundColor Cyan
Write-Host "  Configuration : $Configuration" -ForegroundColor Cyan
Write-Host "  Project       : $Project" -ForegroundColor Cyan
Write-Host ""


# === Paths ===

$TcDir = if ($env:COMMANDER_PATH) { "$env:COMMANDER_PATH" } else { $null }

if (-not $TcDir) {
    throw "The COMMANDER_PATH environment variable is not set."
}

$BaseDir         = Split-Path $PSScriptRoot -Parent
$TcmatchCoreBin  = "$BaseDir\tcmatch.Core\tcmatch.Core\bin\$Configuration"
$Tcmatch32Bin    = "$BaseDir\tcmatch\tcmatch\bin\$Configuration x32"
$Tcmatch64Bin    = "$BaseDir\tcmatch\tcmatch\bin\$Configuration x64"


# === Helper functions ===

function Copy-Required {
    param (
        [Parameter(Mandatory = $true)] [string]$Source,
        [Parameter(Mandatory = $true)] [string]$Destination,
                                       [switch]$IgnoreMissing
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        if ($IgnoreMissing) {
            Write-Host "  [SKIP] $Source not found" -ForegroundColor DarkYellow
            return
        }

        throw "Required file not found: $Source"
    }

    $ParentDir = Split-Path -Path $Destination -Parent

    if (-not (Test-Path -LiteralPath $ParentDir)) {
        New-Item -ItemType Directory -Path $ParentDir -Force | Out-Null
    }

    for ($Attempt = 1; $Attempt -le 30; $Attempt++) {
        try {
            Copy-Item -LiteralPath $Source -Destination $Destination -Force
            Write-Host "  [OK] $Destination" -ForegroundColor Green
            return
        }
        catch {
            if ($Attempt -eq 30) {
                throw
            }

            if ($Attempt -eq 1) {
                Write-Host "  [WAIT] File is still in use, retrying..." -ForegroundColor Yellow
            }

            Start-Sleep -Milliseconds 100
        }
    }
}

function Stop-TotalCommander {
    $Processes = Get-Process -Name "totalcmd", "totalcmd64" -ErrorAction SilentlyContinue

    if ($Processes) {
        Write-Host "Stopping Total Commander..." -ForegroundColor Yellow
        Write-Host ""
        $Processes | Stop-Process -Force

        do {
            Start-Sleep -Milliseconds 100
            $Processes = Get-Process -Name "totalcmd", "totalcmd64" -ErrorAction SilentlyContinue
        } while ($Processes)
    }
}

function Start-TotalCommander {
    if ($Project -eq "tcmatch") {
        Start-Process "$TcDir\totalcmd.exe"
    }
}


# === Debug deployment ===

if ($Configuration -eq "Debug") {

    Write-Host "Deploying debug project to Total Commander..." -ForegroundColor Yellow
    Write-Host ""

    Stop-TotalCommander

    if ($Project -eq "tcmatch.Core") {
        Copy-Required "$TcmatchCoreBin\tcmatch.Core.dll" "$TcDir\QSX2\tcmatch.Core.dll"
    } else {
        Copy-Required "$Tcmatch32Bin\tcmatch.dll"        "$TcDir\QSX2\tcmatch.dll"      -IgnoreMissing
        Copy-Required "$Tcmatch64Bin\tcmatch64.dll"      "$TcDir\QSX2\tcmatch64.dll"    -IgnoreMissing
    }

    Start-TotalCommander

    Write-Host ""
    Write-Host "Debug deployment completed." -ForegroundColor Cyan
    Write-Host ""

    if ($Interactive) {
        Read-Host "Press Enter to exit"
    }

    exit 0
}


# === Release package ===

Write-Host "Building complete Release package..." -ForegroundColor Yellow
Write-Host ""

# 1. Recreate bin directory

if (Test-Path -LiteralPath "$BaseDir\bin") {
    Remove-Item -LiteralPath "$BaseDir\bin" -Recurse -Force
}

# 2. DLLs from tcmatch.Core Release

Copy-Required "$TcmatchCoreBin\tcmatch.Core.dll"                           "$BaseDir\bin\QSX2\tcmatch.Core.dll"
Copy-Required "$TcmatchCoreBin\Markdig.dll"                                "$BaseDir\bin\QSX2\Markdig.dll"
Copy-Required "$TcmatchCoreBin\Wpf.Ui.dll"                                 "$BaseDir\bin\QSX2\Wpf.Ui.dll"
Copy-Required "$TcmatchCoreBin\Wpf.Ui.Abstractions.dll"                    "$BaseDir\bin\QSX2\Wpf.Ui.Abstractions.dll"
Copy-Required "$TcmatchCoreBin\Microsoft.Web.WebView2.Wpf.dll"             "$BaseDir\bin\QSX2\Microsoft.Web.WebView2.Wpf.dll"
Copy-Required "$TcmatchCoreBin\System.Buffers.dll"                         "$BaseDir\bin\QSX2\System.Buffers.dll"
Copy-Required "$TcmatchCoreBin\System.Memory.dll"                          "$BaseDir\bin\QSX2\System.Memory.dll"
Copy-Required "$TcmatchCoreBin\System.Numerics.Vectors.dll"                "$BaseDir\bin\QSX2\System.Numerics.Vectors.dll"
Copy-Required "$TcmatchCoreBin\System.Runtime.CompilerServices.Unsafe.dll" "$BaseDir\bin\QSX2\System.Runtime.CompilerServices.Unsafe.dll"
Copy-Required "$TcmatchCoreBin\Microsoft.Web.WebView2.Core.dll"            "$BaseDir\bin\QSX2\x32\Microsoft.Web.WebView2.Core.dll"
Copy-Required "$TcmatchCoreBin\runtimes\win-x86\native\WebView2Loader.dll" "$BaseDir\bin\QSX2\x32\WebView2Loader.dll"
Copy-Required "$TcmatchCoreBin\Microsoft.Web.WebView2.Core.dll"            "$BaseDir\bin\QSX2\x64\Microsoft.Web.WebView2.Core.dll"
Copy-Required "$TcmatchCoreBin\runtimes\win-x64\native\WebView2Loader.dll" "$BaseDir\bin\QSX2\x64\WebView2Loader.dll"

# 3. DLLs from tcmatch Release

Copy-Required "$Tcmatch32Bin\tcmatch.dll"                                  "$BaseDir\bin\QSX2\tcmatch.dll"
Copy-Required "$Tcmatch64Bin\tcmatch64.dll"                                "$BaseDir\bin\QSX2\tcmatch64.dll"

# 4. Other files

Copy-Required "$BaseDir\data\pluginst.inf"                                 "$BaseDir\bin\pluginst.inf"
Copy-Required "$BaseDir\data\tcmatch.pinyin.tbl"                           "$BaseDir\bin\QSX2\tcmatch.pinyin.tbl"

Copy-Required "$BaseDir\README.md"                                         "$BaseDir\bin\QSX2\tcmatch.readme.en.md"
Copy-Required "$BaseDir\README.de.md"                                      "$BaseDir\bin\QSX2\tcmatch.readme.de.md"

# 5. Deploy complete Release package to Total Commander

Write-Host ""
Write-Host "Deploying complete Release package to Total Commander..." -ForegroundColor Yellow
Write-Host ""

Stop-TotalCommander

Copy-Item -Path "$BaseDir\bin\QSX2\*" -Destination "$TcDir\QSX2\" -Recurse -Force

Write-Host "  [OK] $TcDir\QSX2" -ForegroundColor Green
Write-Host ""

# 6. Create ZIP archive

Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
Write-Host ""

$CurrentDate = Get-Date -Format "yyyy-MM-dd"

Compress-Archive -Path "$BaseDir\bin\pluginst.inf", "$BaseDir\bin\QSX2" -DestinationPath "$BaseDir\bin\QSX2 $CurrentDate.zip" -Force

Start-TotalCommander

Write-Host "Release deployment completed." -ForegroundColor Cyan
Write-Host ""

if ($Interactive) {
    Read-Host "Press Enter to exit"
}
