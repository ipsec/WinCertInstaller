<#
.SYNOPSIS
    Script to install ITI and MPF certificates on Windows.
.EXAMPLE
    .\WinCertInstaller.ps1 -All
    .\WinCertInstaller.ps1 -All -ForceInstall
#>

param (
    [switch]$Iti,
    [switch]$Mpf,
    [switch]$All,
    [switch]$DryRun,
    [switch]$ForceInstall,
    [string]$ItiUrl = "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip",
    [string]$MpfUrl = "http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.p7b",
    [string]$ItiHashUrl = "https://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/hashsha512.txt",
    [string]$MpfHashUrl = "http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.sha512sum",
    [string]$LogPath = "$env:ProgramData\WinCertInstaller\install.log"
)

# ---------------------------------------------------------
# 1. Elevation Check (Self-Restart if needed)
# ---------------------------------------------------------
if (-not $DryRun -and -not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Process is not elevated. Requesting Administrator privileges..."
    
    $argList = @("-File", "`"$PSCommandPath`"")
    foreach ($key in $PSBoundParameters.Keys) {
        $val = $PSBoundParameters[$key]
        if ($val -is [switch]) {
            if ($val) { $argList += "-$key" }
        } else {
            $argList += "-$key"
            $argList += "`"$val`""
        }
    }
    $argList += $MyInvocation.UnboundArguments

    try {
        Start-Process powershell.exe -ArgumentList $argList -Verb RunAs -ErrorAction Stop
        exit
    } catch {
        Write-Error "Failed to request elevation or user cancelled UAC: $($_.Exception.Message)"
        return
    }
}

# ---------------------------------------------------------
# 2. Environment & Logging Setup
# ---------------------------------------------------------

# Force TLS 1.2+ for secure downloads
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Robust UTF-8 configuration
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# Logging Setup (Safe to try creating directory in ProgramData)
try {
    $logDir = [System.IO.Path]::GetDirectoryName($LogPath)
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force -ErrorAction SilentlyContinue | Out-Null }
} catch {
    # If this fails, Write-Log will catch it, so we can ignore it here for now.
}

function Write-Log {
    param (
        [Parameter(Mandatory=$true)]
        [string]$Message,
        [ValidateSet("INFO", "WARN", "ERROR", "SUCCESS", "DRYRUN")]
        [string]$Level = "INFO",
        [ConsoleColor]$Color = "White"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logLine = "$timestamp [$Level] $Message"
    
    # Console Output (Pretty)
    switch ($Level) {
        "WARN"    { Write-Warning $Message }
        "ERROR"   { Write-Error $Message -ErrorAction SilentlyContinue }
        "SUCCESS" { Write-Host $Message -ForegroundColor Green }
        "DRYRUN"  { Write-Host $Message -ForegroundColor Cyan }
        default   { Write-Host $Message -ForegroundColor $Color }
    }
    
    # File Persistence (Audit) - Only attempt if elevated or path is writable
    try {
        $logLine | Out-File -FilePath $LogPath -Append -Encoding UTF8 -ErrorAction Stop
    } catch {
        # Only warn if NOT in a non-elevated dryrun (where failure is expected)
        if (-not $DryRun) {
            Write-Warning "Failed to write to log file: $($_.Exception.Message)"
        }
    }
}

if (-not $Iti -and -not $Mpf -and -not $All) { $All = $true }
if ($All) { $Iti = $true; $Mpf = $true }

function Install-Certs {
    param ([System.Security.Cryptography.X509Certificates.X509Certificate2[]]$Certificates)

    $now = Get-Date
    
    # Store Names (Enum based for robustness)
    $storeRoot = [System.Security.Cryptography.X509Certificates.X509Store]::new([System.Security.Cryptography.X509Certificates.StoreName]::Root, [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    $storeCA = [System.Security.Cryptography.X509Certificates.X509Store]::new([System.Security.Cryptography.X509Certificates.StoreName]::CertificateAuthority, [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    
    try {
        $openMode = if ($DryRun) { "ReadOnly" } else { "ReadWrite" }
        $storeRoot.Open($openMode)
        $storeCA.Open($openMode)

        foreach ($cert in $Certificates) {
            $cn = $cert.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)

            if ($now -lt $cert.NotBefore) {
                Write-Log "Skipped: Certificate '$cn' is not yet active." -Level "WARN"
                continue
            }
            if ($now -gt $cert.NotAfter) {
                Write-Log "Skipped: Certificate '$cn' is expired." -Level "WARN"
                continue
            }

            $isRoot = ($cert.Subject -eq $cert.Issuer)
            $storeName = if ($isRoot) { "Root (Root)" } else { "CA (Intermediate)" }
            $targetStore = if ($isRoot) { $storeRoot } else { $storeCA }

            if (-not $ForceInstall) {
                $existing = $targetStore.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint, $cert.Thumbprint, $false)
                if ($existing.Count -gt 0) {
                    Write-Log "Already Inst.: '$cn' in $storeName." -Color DarkGray
                    continue
                }
            }

            if ($DryRun) {
                Write-Log "Dry-run: Would add '$cn' to $storeName." -Level "DRYRUN"
            }
            else {
                $targetStore.Add($cert)
                Write-Log "$(if($ForceInstall){'Forced'}else{'Installed'}): '$cn' added to $storeName." -Level "SUCCESS"
            }
        }
    }
    finally {
        # 4. Ensure resources are disposed even on error
        $storeRoot.Dispose()
        $storeCA.Dispose()
    }
}

Write-Log "Starting WinCertInstaller process..."

# --- ITI Installation ---
if ($Iti) {
    Write-Log "====================== ITI ======================" -Color Yellow
    Write-Log "Processing ITI certificates..."
    $zipPath = "$env:TEMP\ACcompactado.zip"
    $extractPath = "$env:TEMP\ITI_Certs"
    
    try {
        # 1. Download ITI ZIP and its corresponding SHA512 hash
        Invoke-WebRequest -Uri $ItiUrl -OutFile $zipPath -UseBasicParsing -ErrorAction Stop
        
        $hashPath = "$env:TEMP\iti_hash.txt"
        Invoke-WebRequest -Uri $ItiHashUrl -OutFile $hashPath -UseBasicParsing -ErrorAction Stop
        
        # 2. Extract expected hash (first word/128 chars) and compare
        $expectedHash = (Get-Content $hashPath).Substring(0, 128).Trim()
        $actualHash = (Get-FileHash -Path $zipPath -Algorithm SHA512).Hash
        
        if ($expectedHash -ne $actualHash) {
            Write-Log "Hash mismatch! Expected: $expectedHash, Actual: $actualHash" -Level "ERROR"
            return
        }
        Write-Log "SHA512 Verification Successful." -Level "SUCCESS"
        
        if (Test-Path $extractPath) { Remove-Item -Path $extractPath -Recurse -Force }
        Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
        
        $certFiles = Get-ChildItem -Path $extractPath -Include "*.cer", "*.crt" -Recurse
        
        # 5. Optimize memory: capture loop output
        $certs = foreach ($file in $certFiles) {
            [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($file.FullName)
        }
        
        if ($certs) {
            Install-Certs -Certificates $certs
        }
    }
    catch {
        Write-Log "Failed to process ITI certificates: $($_.Exception.Message)" -Level "ERROR"
    }
    finally {
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
    }
}

# --- MPF Installation ---
if ($Mpf) {
    Write-Log "====================== MPF ======================" -Color Yellow
    Write-Log "Processing MPF certificates..."
    $p7bPath = "$env:TEMP\ACIMPF.p7b"
    
    try {
        # 1. Download MPF P7B and its corresponding SHA512 hash
        Invoke-WebRequest -Uri $MpfUrl -OutFile $p7bPath -UseBasicParsing -ErrorAction Stop
        
        $hashPath = "$env:TEMP\mpf_hash.txt"
        Invoke-WebRequest -Uri $MpfHashUrl -OutFile $hashPath -UseBasicParsing -ErrorAction Stop
        
        # 2. Extract expected hash (first word/128 chars) and compare
        $expectedHash = (Get-Content $hashPath).Substring(0, 128).Trim()
        $actualHash = (Get-FileHash -Path $p7bPath -Algorithm SHA512).Hash
        
        if ($expectedHash -ne $actualHash) {
            Write-Log "Hash mismatch for MPF certificates! Expected: $expectedHash, Actual: $actualHash" -Level "ERROR"
            return
        }
        Write-Log "SHA512 Verification Successful (MPF)." -Level "SUCCESS"
        
        $certCollection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
        $certCollection.Import($p7bPath)
        
        $certs = foreach ($cert in $certCollection) {
            $cert
        }

        if ($certs) {
            Install-Certs -Certificates $certs
        }
    }
    catch {
        Write-Log "Failed to process MPF certificates: $($_.Exception.Message)" -Level "ERROR"
    }
    finally {
        if (Test-Path $p7bPath) { Remove-Item $p7bPath -Force }
    }
}

Write-Log "=================================================" -Color Yellow
Write-Log "Process completed."