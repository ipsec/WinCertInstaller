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

# 1. Force TLS 1.2+ for secure downloads
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# 2. Robust UTF-8 configuration for PowerShell 5.1
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# Logging Setup
$logDir = [System.IO.Path]::GetDirectoryName($LogPath)
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

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
    
    # File Persistence (Audit)
    try {
        $logLine | Out-File -FilePath $LogPath -Append -Encoding UTF8
    } catch {
        Write-Warning "Failed to write to log file: $($_.Exception.Message)"
    }
}

# 3. Check for Administrator privileges upfront (unless DryRun)
if (-not $DryRun -and -not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Log "This script must be executed as Administrator for certificate store operations." -Level "ERROR"
    return
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
    Write-Host "====================== ITI ======================" -ForegroundColor Yellow
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
    Write-Host "====================== MPF ======================" -ForegroundColor Yellow
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

Write-Host "=================================================" -ForegroundColor Yellow
Write-Log "Process completed."