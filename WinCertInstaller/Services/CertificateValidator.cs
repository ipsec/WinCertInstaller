using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using WinCertInstaller.Logging;

namespace WinCertInstaller.Services
{
    public class CertificateValidator : ICertificateValidator
    {
        private readonly SimpleLogger<CertificateValidator> _logger;

        public CertificateValidator(SimpleLogger<CertificateValidator> logger)
        {
            _logger = logger;
        }
        public bool IsCertificateValidForInstall(X509Certificate2 certificate)
        {
            if (certificate.NotBefore > DateTime.UtcNow)
            {
                _logger.LogWarning("Certificate {Subject} not active yet. NotBefore={NotBefore}", certificate.Subject, certificate.NotBefore);
                return false;
            }

            if (certificate.NotAfter < DateTime.UtcNow)
            {
                _logger.LogWarning("Certificate {Subject} expired. NotAfter={NotAfter}", certificate.Subject, certificate.NotAfter);
                return false;
            }

            return true;
        }

        public bool IsCertificateAuthority(X509Certificate2 certificate)
        {
            return certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(ext => ext.CertificateAuthority);
        }

        public bool IsSelfSigned(X509Certificate2 certificate)
        {
            return certificate.SubjectName.RawData.SequenceEqual(certificate.IssuerName.RawData);
        }
    }
}
