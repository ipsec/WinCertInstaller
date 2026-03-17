# WinCertInstaller

[![.NET](https://github.com/fernandoribeiro/WinCertInstaller/actions/workflows/dotnet.yml/badge.svg)](https://github.com/fernandoribeiro/WinCertInstaller/actions/workflows/dotnet.yml)

This repository contains a C# (.NET 10) application which downloads and installs the official Root and Intermediate Certificates from **ITI (ICP-Brasil)** and **MPF** into the Windows Trusted Root Store.

*⚠️ Needs **Administrator privileges** to run, as it writes directly to the `LocalMachine` X509 Store.*

## Features

* **Automated Downloads**: Fetches the latest `.zip` (ITI) and `.p7b` (MPF) certificate bundles.
* **Smart Validation**: Validates expiration dates, ensures the certificate is active, and separates Self-Signed (Root) from Intermediate CAs.
* **Resilience**: Features automatic retries and cancellation tokens for network requests.
* **Idempotency**: Skips already installed certificates without throwing duplicate errors.
* **Dry-Run Mode**: Allows testing the extraction and validation process without writing anything to the OS registry.

## Architecture & SOLID

The project is structured into clear responsibilities:
* `Models/`: Data structures and Enums (e.g., `CertSource`).
* `Configuration/`: Constant settings and URLs.
* `Services/`: Core business logic broken down into:
  * `ICertificateDownloader`: Handles HTTP streams and archive extractions.
  * `ICertificateValidator`: Enforces cryptographic rules.
  * `ICertificateInstaller`: Interfaces with the Windows `X509Store`.

## Usage

You can run the application directly from the command line:

```console
Usage: WinCertInstaller [options]
Options:
  --iti        Install certificates from ITI
  --mpf        Install certificates from MPF
  --all        Install certificates from ITI and MPF (default)
  --dry-run    Run without writing certificates to store
  -q           Quiet mode (no pause at exit)
  -h,--help    Show this help message
```

*Example: Testing ITI extraction without installing*  
```powershell
WinCertInstaller.exe --iti --dry-run
```

## Building and Testing

To compile the application and run its unit tests:

```powershell
dotnet build WinCertInstaller\WinCertInstaller.csproj
dotnet test WinCertInstaller.Tests\WinCertInstaller.Tests.csproj
```

*Use at your own risk. Feel free to make pull-requests :)*
