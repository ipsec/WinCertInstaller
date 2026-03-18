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
    [string]$MpfUrl = "http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.p7b"
)

# 1. Force TLS 1.2+ for secure downloads
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# 2. Robust UTF-8 configuration for PowerShell 5.1
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# 3. Check for Administrator privileges upfront
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be executed as Administrator to write to the LocalMachine store."
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
                Write-Warning "Skipped      : Certificate '$cn' is not yet active."
                continue
            }
            if ($now -gt $cert.NotAfter) {
                Write-Warning "Skipped      : Certificate '$cn' is expired."
                continue
            }

            $isRoot = ($cert.Subject -eq $cert.Issuer)
            $storeName = if ($isRoot) { "Root (Root)" } else { "CA (Intermediate)" }
            $targetStore = if ($isRoot) { $storeRoot } else { $storeCA }

            if (-not $ForceInstall) {
                $existing = $targetStore.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint, $cert.Thumbprint, $false)
                if ($existing.Count -gt 0) {
                    Write-Host "Already Inst.: '$cn' in $storeName." -ForegroundColor DarkGray
                    continue
                }
            }

            if ($DryRun) {
                Write-Host "Dry-run      : Would add '$cn' to $storeName." -ForegroundColor Cyan
            }
            else {
                $targetStore.Add($cert)
                if ($ForceInstall) {
                    Write-Host "Forced       : '$cn' injected into $storeName." -ForegroundColor Magenta
                }
                else {
                    Write-Host "Installed    : '$cn' added to $storeName." -ForegroundColor Green
                }
            }
        }
    }
    finally {
        # 4. Ensure resources are disposed even on error
        $storeRoot.Dispose()
        $storeCA.Dispose()
    }
}

# --- ITI Installation ---
if ($Iti) {
    Write-Host "====================== ITI ======================" -ForegroundColor Yellow
    Write-Host "Downloading and processing ITI certificates..."
    $zipPath = "$env:TEMP\ACcompactado.zip"
    $extractPath = "$env:TEMP\ITI_Certs"
    
    try {
        Invoke-WebRequest -Uri $ItiUrl -OutFile $zipPath -UseBasicParsing -ErrorAction Stop
        
        if (Test-Path $extractPath) { Remove-Item -Path $extractPath -Recurse -Force }
        Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
        
        $certFiles = Get-ChildItem -Path $extractPath -Include "*.cer", "*.crt" -Recurse
        
        # 5. Optimize memory: capture loop output instead of using +=
        $certs = foreach ($file in $certFiles) {
            [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($file.FullName)
        }
        
        if ($certs) {
            Install-Certs -Certificates $certs
        }
    }
    catch {
        Write-Error "Failed to process ITI certificates: $($_.Exception.Message)"
    }
    finally {
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
    }
}

# --- MPF Installation ---
if ($Mpf) {
    Write-Host "====================== MPF ======================" -ForegroundColor Yellow
    Write-Host "Downloading and processing MPF certificates..."
    $p7bPath = "$env:TEMP\ACIMPF.p7b"
    
    try {
        Invoke-WebRequest -Uri $MpfUrl -OutFile $p7bPath -UseBasicParsing -ErrorAction Stop
        
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
        Write-Error "Failed to process MPF certificates: $($_.Exception.Message)"
    }
    finally {
        if (Test-Path $p7bPath) { Remove-Item $p7bPath -Force }
    }
}

Write-Host "=================================================" -ForegroundColor Yellow
Write-Host "Process completed."