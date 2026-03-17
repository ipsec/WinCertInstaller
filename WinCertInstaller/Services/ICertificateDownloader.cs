using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace WinCertInstaller.Services
{
    public interface ICertificateDownloader
    {
        Task<X509Certificate2Collection> GetZIPCertificatesAsync(string url, CancellationToken cancellationToken = default);
        Task<X509Certificate2Collection> GetP7BCertificatesAsync(string url, CancellationToken cancellationToken = default);
    }
}
