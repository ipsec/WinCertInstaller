using System.Security.Cryptography.X509Certificates;

namespace WinCertInstaller.Services
{
    public interface ICertificateValidator
    {
        bool IsCertificateValidForInstall(X509Certificate2 certificate);
        bool IsCertificateAuthority(X509Certificate2 certificate);
        bool IsSelfSigned(X509Certificate2 certificate);
    }
}
