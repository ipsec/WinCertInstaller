# WinCertInstaller 🛡️

[![.NET](https://github.com/fernandoribeiro/WinCertInstaller/actions/workflows/dotnet.yml/badge.svg)](https://github.com/fernandoribeiro/WinCertInstaller/actions/workflows/dotnet.yml)

WinCertInstaller is an enterprise-grade C# (.NET 10) utility designed to automate the download and installation of official Root and Intermediate Certificates from **ITI (ICP-Brasil)** and **MPF** into the Windows Certificate Store.

> [!IMPORTANT]
> **Administrator Privileges Required**: This application requires elevated privileges to write certificates to the `LocalMachine` X509 store. It includes an `app.manifest` to automatically request UAC elevation if not already running as Administrator.

## 🚀 Key Features

*   **Automated Certificate Fetching**: Downloads the latest `.zip` (ITI) and `.p7b` (MPF) bundles directly from official repositories.
*   **Robust Decoding**: Handles various formats, including **PEM-encoded PKCS#7** payloads (MPF) and nested ZIP archives (ITI).
*   **Intelligent Validation**: 
    *   Filters for Certificate Authorities (CA).
    *   Distinguishes between Root CAs (installed in `Trusted Root`) and Intermediate CAs (installed in `Intermediate Certification Authorities`).
    *   Checks for expiration and activation dates.
*   **Idempotency**: Detects and skips certificates already present in the store to avoid duplication.
*   **Modern CLI Experience**: Features clean, color-coded console output using a custom `ILogger` formatter.
*   **Dry-Run Mode**: Validate the entire download and extraction process without modifying the system state.

## 🏗️ Architecture (SOLID & Enterprise)

The application has been refactored to follow modern .NET best practices:
*   **Microsoft.Extensions.Hosting**: Uses the Generic Host pattern for dependency injection, logging, and configuration management.
*   **Dynamic Configuration**: All certificate URLs and settings are managed via `appsettings.json`.
*   **Structured Logging**: Utilizes `ILogger<T>` for clean, maintainable logging that can be easily redirected to cloud providers or files.
*   **Dependency Injection**: Decoupled services for Downloading, Validation, and Installation, making the codebase highly testable and maintainable.

## 💻 Compatibility

*   **Runtime**: .NET 10.0+ (Windows-specific payload).
*   **Operating Systems**: 
    *   **Windows 10 / 11** (Fully supported, native target).
    *   **Windows Server 2016 / 2019 / 2022**.
    *   *Note: Requires administrator access for LocalMachine store operations.*

## 🛠️ Usage

```console
Usage: WinCertInstaller [options]

Options:
  --iti        Install ITI certificates
  --mpf        Install MPF certificates
  --all        Install both ITI and MPF certificates (default)
  --dry-run    Simulate installation without writing to the store
  -q           Quiet mode (suppress exit prompt)
  -h,--help    Show this help message
```

### Examples

**Standard Installation (All Sources):**
```powershell
WinCertInstaller.exe
```

**Dry-Run of ITI Source:**
```powershell
WinCertInstaller.exe --iti --dry-run
```

**Quiet Installation (Script Mode):**
```powershell
WinCertInstaller.exe --all -q
```

## 🧪 Development

### Configuration
Edit `appsettings.json` to update certificate URLs or tune logging behavior without recompiling.

### Build & Test
```powershell
# Restore and build the solution
dotnet build WinCertInstaller.sln

# Run unit tests
dotnet test WinCertInstaller.sln
```

### CI/CD
A GitHub Actions workflow is included (`.github/workflows/dotnet.yml`) that automatically builds, tests, and publishes a self-contained, single-file executable on every push to `main`/`master`.

---
*Maintained with ❤️ for secure Windows environments.*
