using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace WinCertInstaller.Services
{
    public class CertificateValidator : ICertificateValidator
    {
        public bool IsCertificateValidForInstall(X509Certificate2 certificate)
        {
            if (certificate.NotBefore > DateTime.UtcNow)
            {
                Console.WriteLine("Certificate {0} not active yet. NotBefore={1}", certificate.Subject, certificate.NotBefore);
                return false;
            }

            if (certificate.NotAfter < DateTime.UtcNow)
            {
                Console.WriteLine("Certificate {0} expired. NotAfter={1}", certificate.Subject, certificate.NotAfter);
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
