using System.Security.Cryptography.X509Certificates;

namespace WinCertInstaller.Services
{
    /// <summary>
    /// Service that handles writing certificates into the Windows Local Machine X509 Store.
    /// </summary>
    public interface ICertificateInstaller
    {
        /// <summary>
        /// Iterates through a collection and installs certificates into their respective Windows Store 
        /// (Root for self-signed CAs or CertificateAuthority for Intermediate CAs).
        /// </summary>
        /// <param name="certificates">The collection of certificates to evaluate and install.</param>
        /// <param name="dryRun">If true, validates and logs actions without making actual changes to the machine store.</param>
        void InstallCertificates(X509Certificate2Collection certificates, bool dryRun);
    }
}
