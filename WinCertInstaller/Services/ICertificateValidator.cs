using System.Security.Cryptography.X509Certificates;

namespace WinCertInstaller.Services
{
    /// <summary>
    /// Validates X509 certificates ensuring they are valid for installation and determines their roles in a PKI.
    /// </summary>
    public interface ICertificateValidator
    {
        /// <summary>
        /// Checks if a certificate is currently active and has not expired based on its NotBefore/NotAfter dates.
        /// </summary>
        bool IsCertificateValidForInstall(X509Certificate2 certificate);

        /// <summary>
        /// Verifies whether the provided certificate is a Certificate Authority (CA) via its Basic Constraints extension.
        /// </summary>
        bool IsCertificateAuthority(X509Certificate2 certificate);

        /// <summary>
        /// Scans if the certificate's Subject matches its Issuer, indicating it is a Self-Signed Root Certificate.
        /// </summary>
        bool IsSelfSigned(X509Certificate2 certificate);
    }
}
