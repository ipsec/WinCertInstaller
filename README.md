# WinCertInstaller 🛡️

WinCertInstaller is a native PowerShell utility designed to automate the download and installation of official Root and Intermediate Certificates from **ITI (ICP-Brasil)** and **MPF** into the Windows Certificate Store.

It was developed to be lightweight (only **~6 KB**) and requires no compilation or external dependencies, leveraging native Windows APIs and the .NET framework already built into PowerShell.

> [!IMPORTANT]
> **Administrator Privileges Required**: This script requires elevated privileges to write certificates to the `LocalMachine` X509 store. Run your PowerShell terminal as Administrator.

## 🚀 Key Features

*   **Ultra-Lightweight**: Only a few KB of native PowerShell code.
*   **Automated Certificate Fetching**: Downloads the latest `.zip` (ITI) and `.p7b` (MPF) bundles directly from official repositories.
*   **Data Integrity (SHA512)**: Verifies both ITI and MPF certificate bundles against their official SHA512 hash files before processing.
*   **Enterprise-Ready Robustness**:
    *   **Audit Logging**: Automatically records all operations (with timestamps and severity) to `%ProgramData%\WinCertInstaller\install.log` for post-deployment auditing.
    *   **Security**: Forces **TLS 1.2+** for all downloads.
    *   **Pre-emptive Admin Check**: Validates permissions before starting long operations.
    *   **Resource Management**: Uses `.Dispose()` in `finally` blocks to ensure system resources are freed.
    *   **Performance**: Optimized memory usage using captured loops instead of array re-allocation.
    *   **Error Resilience**: Comprehensive `try-catch` blocks for network and extraction failures.
*   **Highly Configurable**: Official repository URLs are available as parameters, making the script future-proof.
*   **Intelligent Store Detection**: Automatically distinguishes between Root CAs and Intermediate CAs.
*   **Idempotency & Reinstallation**: Detects if a certificate is missing from its correct store and reinstalls it even if it exists in another store.
*   **Force Install**: Option to force reinstallation of all certificates regardless of their current status.

## 🛠️ Usage

```powershell
.\wincertinstall.ps1 [options]
```

### Options
*   `-Iti`: Install ITI certificates.
*   `-Mpf`: Install MPF certificates.
*   `-All`: Install both ITI and MPF certificates (default).
*   `-DryRun`: Simulate installation without modifying the store.
*   `-ForceInstall`: Force installation of all certificates, ignoring existing ones.

### Examples

**Standard Installation (All Sources):**
```powershell
.\wincertinstall.ps1
```

**Force Reinstallation of everything:**
```powershell
.\wincertinstall.ps1 -All -ForceInstall
```

**Dry-Run (Simulation) of ITI Source:**
```powershell
.\wincertinstall.ps1 -Iti -DryRun
```

## 💻 Compatibility

*   **PowerShell Versions**: Optimized for **PowerShell 5.1** (Windows default) and **PowerShell Core (6+)**.
*   **Operating Systems**: 
    *   Windows 10 / 11.
    *   Windows Server 2016 / 2019 / 2022.
    *   *Note: Requires administrator access for LocalMachine store operations.*

---
*Maintained with ❤️ for secure Windows environments.*
