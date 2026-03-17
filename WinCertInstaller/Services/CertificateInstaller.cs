using System;
using System.Security.Cryptography.X509Certificates;

namespace WinCertInstaller.Services
{
    public class CertificateInstaller : ICertificateInstaller
    {
        private readonly ICertificateValidator _validator;

        public CertificateInstaller(ICertificateValidator validator)
        {
            _validator = validator;
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

            Console.WriteLine("Installing certificates into {0}.", storeName);

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
                    added++;
                    continue;
                }

                store.Add(certificate);
                added++;
            }

            Console.WriteLine("Added {0} certificates to {1}.", added, storeName);
            if (skipped > 0) Console.WriteLine("Skipped {0} already installed certificate(s).", skipped);
            if (invalid > 0) Console.WriteLine("Ignored {0} invalid certificate(s).", invalid);

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
                    Console.WriteLine("No CA certificates to import.");
                }

                if (caIntermediateCertificates.Count > 0)
                {
                    Add(caIntermediateCertificates, StoreName.CertificateAuthority, StoreLocation.LocalMachine, dryRun);
                }
                else
                {
                    Console.WriteLine("No Intermediate CA certificates to import.");
                }
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Console.WriteLine("Erro ao acessar os repositórios do Windows: {0}", ex.Message);
                Console.WriteLine("DICA: Certifique-se de executar este programa como 'Administrador' para instalar certificados na máquina local.");
            }
        }
    }
}
