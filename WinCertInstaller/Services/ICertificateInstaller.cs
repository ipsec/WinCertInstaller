using System.Security.Cryptography.X509Certificates;

namespace WinCertInstaller.Services
{
    public interface ICertificateInstaller
    {
        void InstallCertificates(X509Certificate2Collection certificates, bool dryRun);
    }
}
