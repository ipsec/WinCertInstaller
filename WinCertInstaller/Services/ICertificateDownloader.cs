using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace WinCertInstaller.Services
{
    /// <summary>
    /// Service responsible for downloading and extracting certificates from remote sources.
    /// </summary>
    public interface ICertificateDownloader
    {
        /// <summary>
        /// Downloads a ZIP archive and extracts all its certificates (.cer or .crt).
        /// </summary>
        /// <param name="url">The URL of the ZIP file.</param>
        /// <param name="cancellationToken">Token to cancel the download operation.</param>
        /// <returns>A collection of extracted certificates.</returns>
        Task<X509Certificate2Collection> GetZIPCertificatesAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a PKCS #7 (.p7b) file and extracts its certificates.
        /// </summary>
        /// <param name="url">The URL of the P7B file.</param>
        /// <param name="cancellationToken">Token to cancel the download operation.</param>
        /// <returns>A collection of extracted certificates.</returns>
        Task<X509Certificate2Collection> GetP7BCertificatesAsync(string url, CancellationToken cancellationToken = default);
    }
}
