# WinCertInstaller 🛡️

WinCertInstaller is a native PowerShell utility designed to automate the download and installation of official Root and Intermediate Certificates from **ITI (ICP-Brasil)** and **MPF** into the Windows Certificate Store.

It was developed to be lightweight (only **~6 KB**) and requires no compilation or external dependencies, leveraging native Windows APIs and the .NET framework already built into PowerShell.

> [!IMPORTANT]
> **Administrator Privileges Required**: This script requires elevated privileges to write certificates to the `LocalMachine` X509 store. Run your PowerShell terminal as Administrator.

## 🚀 Key Features

*   **Ultra-Lightweight**: Only a few KB of native PowerShell code.
*   **Automated Certificate Fetching**: Downloads the latest `.zip` (ITI) and `.p7b` (MPF) bundles directly from official repositories.
*   **Robust Decoding**: Handles **PEM-encoded** certificates and PKCS#7 payloads.
*   **Intelligent Store Detection**: Automatically distinguishes between Root CAs (installed in `Trusted Root`) and Intermediate CAs (installed in `Intermediate Certification Authorities`).
*   **UTF-8 Support**: Full support for accented characters in the console.
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
