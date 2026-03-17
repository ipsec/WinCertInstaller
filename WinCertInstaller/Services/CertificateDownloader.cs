using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace WinCertInstaller.Services
{
    public class CertificateDownloader : ICertificateDownloader
    {
        private readonly ILogger<CertificateDownloader> _logger;
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public CertificateDownloader(ILogger<CertificateDownloader> logger)
        {
            _logger = logger;
        }

        private async Task<MemoryStream?> DownloadFileAsync(string url, CancellationToken cancellationToken = default, int maxAttempts = 3, TimeSpan? delayBetweenAttempts = null)
        {
            delayBetweenAttempts ??= TimeSpan.FromSeconds(2);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    
                    var memoryStream = new MemoryStream();
                    await response.Content.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Position = 0;
                    return memoryStream;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Download canceled.");
                    return null;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    if (cancellationToken.IsCancellationRequested) return null;
                    _logger.LogWarning(ex, "Attempt {Attempt} failed for {Url}: {Message}", attempt, url, ex.Message);
                    await Task.Delay(delayBetweenAttempts.Value, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to download certificates from {Url} after {Attempt} attempts. Error: {Message}", url, attempt, ex.Message);
                }
            }

            return null;
        }

        public async Task<X509Certificate2Collection> GetZIPCertificatesAsync(string url, CancellationToken cancellationToken = default)
        {
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            _logger.LogInformation("Downloading certificates from {Url}. Please wait...", url);

            using MemoryStream? stream = await DownloadFileAsync(url, cancellationToken);

            if (stream != null)
            {
                using var archive = new ZipArchive(stream);

                foreach (ZipArchiveEntry certificate in archive.Entries)
                {
                    if (certificate.Length == 0 || !(certificate.FullName.EndsWith(".cer", StringComparison.OrdinalIgnoreCase) || certificate.FullName.EndsWith(".crt", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    try
                    {
                        using Stream certStream = certificate.Open();
                        using MemoryStream ms = new MemoryStream();
                        certStream.CopyTo(ms);

                        var cert = X509CertificateLoader.LoadCertificate(ms.ToArray());
                        certCollection.Add(cert);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load certificate from zip entry {FullName}: {Message}", certificate.FullName, ex.Message);
                    }
                }
                _logger.LogInformation("Found {Count} certificate(s) in ZIP archive.", certCollection.Count);
            }
            return certCollection;
        }

        public async Task<X509Certificate2Collection> GetP7BCertificatesAsync(string url, CancellationToken cancellationToken = default)
        {
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            _logger.LogInformation("Downloading certificates from {Url}. Please wait...", url);

            using MemoryStream? stream = await DownloadFileAsync(url, cancellationToken);

            if (stream != null)
            {
                byte[] rawBytes = stream.ToArray();
                string text = System.Text.Encoding.UTF8.GetString(rawBytes);

                // Check if the PKCS#7 is PEM encoded and decode it to raw DER format
                if (text.Contains("-----BEGIN PKCS7-----"))
                {
                    string base64 = text.Replace("-----BEGIN PKCS7-----", "")
                                        .Replace("-----END PKCS7-----", "")
                                        .Replace("\r", "")
                                        .Replace("\n", "")
                                        .Trim();
                    rawBytes = Convert.FromBase64String(base64);
                }

                var signedCms = new SignedCms();
                signedCms.Decode(rawBytes);

                foreach (var cert in signedCms.Certificates)
                {
                    certCollection.Add(cert);
                }

                _logger.LogInformation("Extracted {Count} certificate(s) from P7B/PKCS7 payload.", certCollection.Count);
            }
            return certCollection;
        }
    }
}
