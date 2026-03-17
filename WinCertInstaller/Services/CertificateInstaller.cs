using System;
using System.Security.Cryptography.X509Certificates;
using WinCertInstaller.Logging;

namespace WinCertInstaller.Services
{
    public class CertificateInstaller : ICertificateInstaller
    {
        private readonly ICertificateValidator _validator;
        private readonly SimpleLogger<CertificateInstaller> _logger;

        public CertificateInstaller(ICertificateValidator validator, SimpleLogger<CertificateInstaller> logger)
        {
            _validator = validator;
            _logger = logger;
        }

        private bool IsCertificateInStore(X509Certificate2 certificate, X509Store store)
        {
            foreach (var existingCert in store.Certificates)
            {
                if (string.Equals(existingCert.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void Add(X509Certificate2Collection certificates, StoreName storeName, StoreLocation location, bool dryRun)
        {
            using var store = new X509Store(storeName, location);
            store.Open(dryRun ? OpenFlags.ReadOnly : OpenFlags.ReadWrite);

            _logger.LogInformation("Installing certificates into the {StoreName} store...", storeName);

            int added = 0;
            int skipped = 0;
            int invalid = 0;

            foreach (X509Certificate2 certificate in certificates)
            {
                if (!_validator.IsCertificateValidForInstall(certificate))
                {
                    invalid++;
                    continue;
                }

                if (IsCertificateInStore(certificate, store))
                {
                    skipped++;
                    continue;
                }

                if (dryRun)
                {
                    _logger.LogInformation("Dry-run: Would add {Subject} to {StoreName}", certificate.Subject, storeName);
                    added++;
                    continue;
                }

                store.Add(certificate);
                _logger.LogInformation("Added: {Subject} to {StoreName}", certificate.Subject, storeName);
                added++;
            }

            _logger.LogInformation("Successfully processed {Added} certificate(s) for {StoreName}.", added, storeName);
            if (skipped > 0) _logger.LogInformation("Skipped {Skipped} certificate(s) already present in the store.", skipped);
            if (invalid > 0) _logger.LogWarning("Ignored {Invalid} invalid or ineligible certificate(s).", invalid);

            store.Close();
        }

        public void InstallCertificates(X509Certificate2Collection certificates, bool dryRun)
        {
            X509Certificate2Collection caCertificates = new X509Certificate2Collection();
            X509Certificate2Collection caIntermediateCertificates = new X509Certificate2Collection();

            foreach (X509Certificate2 cert in certificates)
            {
                bool isCA = _validator.IsCertificateAuthority(cert);
                bool isSelfSigned = _validator.IsSelfSigned(cert);
                if (isCA)
                {
                    if (isSelfSigned)
                    {
                        caCertificates.Add(cert);
                    }
                    else
                    {
                        caIntermediateCertificates.Add(cert);
                    }
                }
                else
                {
                    // Ignora certificados que não são identificados como Autoridade Certificadora
                }
            }

            try
            {
                if (caCertificates.Count > 0)
                {
                    Add(caCertificates, StoreName.Root, StoreLocation.LocalMachine, dryRun);
                }
                else
                {
                    _logger.LogInformation("No CA certificates to import.");
                }

                if (caIntermediateCertificates.Count > 0)
                {
                    Add(caIntermediateCertificates, StoreName.CertificateAuthority, StoreLocation.LocalMachine, dryRun);
                }
                else
                {
                    _logger.LogInformation("No Intermediate CA certificates to import.");
                }
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                _logger.LogError(ex, "Error accessing Windows certificate stores: {Message}", ex.Message);
                _logger.LogWarning("HINT: Ensure you are running this application as 'Administrator' to manage LocalMachine certificates.");
            }
        }
    }
}
