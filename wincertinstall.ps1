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
    [switch]$ForceInstall
)

# Robust UTF-8 configuration for PowerShell 5.1
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if (-not $Iti -and -not $Mpf -and -not $All) { $All = $true }
if ($All) { $Iti = $true; $Mpf = $true }

$ItiUrl = "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip"
$MpfUrl = "http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.p7b"

function Install-Certs {
    param ([System.Security.Cryptography.X509Certificates.X509Certificate2[]]$Certificates)

    $now = Get-Date
    
    $storeRoot = [System.Security.Cryptography.X509Certificates.X509Store]::new([System.Security.Cryptography.X509Certificates.StoreName]::Root, [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    $storeCA = [System.Security.Cryptography.X509Certificates.X509Store]::new([System.Security.Cryptography.X509Certificates.StoreName]::CertificateAuthority, [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    
    $openMode = if ($DryRun) { "ReadOnly" } else { "ReadWrite" }

    try {
        $storeRoot.Open($openMode)
        $storeCA.Open($openMode)
    }
    catch {
        Write-Warning "ERROR: Could not open Windows certificate store. Run as Administrator!"
        return
    }

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

        # Define the target store based on certificate properties
        $isRoot = ($cert.Subject -eq $cert.Issuer)
        $storeName = if ($isRoot) { "Root (Root)" } else { "CA (Intermediate)" }
        $targetStore = if ($isRoot) { $storeRoot } else { $storeCA }

        # Check if it exists in the target store
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

    $storeRoot.Close()
    $storeCA.Close()
}

# --- ITI Installation ---
if ($Iti) {
    Write-Host "====================== ITI ======================" -ForegroundColor Yellow
    Write-Host "Downloading and processing ITI certificates..."
    $zipPath = "$env:TEMP\ACcompactado.zip"
    $extractPath = "$env:TEMP\ITI_Certs"
    
    Invoke-WebRequest -Uri $ItiUrl -OutFile $zipPath -UseBasicParsing
    
    if (Test-Path $extractPath) { Remove-Item -Path $extractPath -Recurse -Force }
    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
    
    $certFiles = Get-ChildItem -Path $extractPath -Include "*.cer", "*.crt" -Recurse
    $certs = @()
    foreach ($file in $certFiles) {
        $certs += [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($file.FullName)
    }
    
    Install-Certs -Certificates $certs
    
    Remove-Item $zipPath -Force
    Remove-Item $extractPath -Recurse -Force
}

# --- MPF Installation ---
if ($Mpf) {
    Write-Host "====================== MPF ======================" -ForegroundColor Yellow
    Write-Host "Downloading and processing MPF certificates..."
    $p7bPath = "$env:TEMP\ACIMPF.p7b"
    
    Invoke-WebRequest -Uri $MpfUrl -OutFile $p7bPath -UseBasicParsing
    
    $certCollection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
    $certCollection.Import($p7bPath)
    
    $certs = @()
    foreach ($cert in $certCollection) {
        $certs += $cert
    }

    Install-Certs -Certificates $certs
    
    Remove-Item $p7bPath -Force
}

Write-Host "=================================================" -ForegroundColor Yellow
Write-Host "Process completed."