<#
.SYNOPSIS
    Script para instalar certificados ITI e MPF no Windows.
.EXAMPLE
    .\WinCertInstaller.ps1 -All
    .\WinCertInstaller.ps1 -All -ForceInstall
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

param (
    [switch]$Iti,
    [switch]$Mpf,
    [switch]$All,
    [switch]$DryRun,
    [switch]$ForceInstall
)

if (-not $Iti -and -not $Mpf -and -not $All) { $All = $true }
if ($All) { $Iti = $true; $Mpf = $true }

$ItiUrl = "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip"
$MpfUrl = "http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.p7b"

function Install-Certs {
    param ([System.Security.Cryptography.X509Certificates.X509Certificate2[]]$Certificates)

    $now = Get-Date
    
    $storeRoot = [System.Security.Cryptography.X509Certificates.X509Store]::new("Root", "LocalMachine")
    $storeCA = [System.Security.Cryptography.X509Certificates.X509Store]::new("CertificateAuthority", "LocalMachine")
    
    $openMode = if ($DryRun) { "ReadOnly" } else { "ReadWrite" }

    try {
        $storeRoot.Open($openMode)
        $storeCA.Open($openMode)
    }
    catch {
        Write-Warning "ERRO: Não foi possível abrir o repositório do Windows. Execute como Administrador!"
        return
    }

    foreach ($cert in $Certificates) {
        $cn = $cert.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)

        if ($now -lt $cert.NotBefore) {
            Write-Warning "Ignorado: Certificado '$cn' ainda não está ativo."
            continue
        }
        if ($now -gt $cert.NotAfter) {
            Write-Warning "Ignorado: Certificado '$cn' está expirado."
            continue
        }

        $isRoot = ($cert.Subject -eq $cert.Issuer)
        $storeName = if ($isRoot) { "Root (Raiz)" } else { "CA (Intermediária)" }
        $targetStore = if ($isRoot) { $storeRoot } else { $storeCA }

        # Se NÃO for ForceInstall, verifica se já existe
        if (-not $ForceInstall) {
            $existingCerts = $targetStore.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint, $cert.Thumbprint, $false)

            if ($existingCerts.Count -gt 0) {
                Write-Host "Já instalado : '$cn' em $storeName." -ForegroundColor DarkGray
                continue
            }
        }

        if ($DryRun) {
            Write-Host "Dry-run      : Adicionaria '$cn' em $storeName." -ForegroundColor Cyan
        }
        else {
            $targetStore.Add($cert)
            if ($ForceInstall) {
                Write-Host "Forçado      : '$cn' injetado em $storeName." -ForegroundColor Magenta
            }
            else {
                Write-Host "Instalado    : '$cn' adicionado em $storeName." -ForegroundColor Green
            }
        }
    }

    $storeRoot.Close()
    $storeCA.Close()
}

# --- Instalação ITI ---
if ($Iti) {
    Write-Host "====================== ITI ======================" -ForegroundColor Yellow
    Write-Host "Baixando e processando certificados ITI..."
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

# --- Instalação MPF ---
if ($Mpf) {
    Write-Host "====================== MPF ======================" -ForegroundColor Yellow
    Write-Host "Baixando e processando certificados MPF..."
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
Write-Host "Processo concluído."