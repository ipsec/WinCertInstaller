using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace WinCertInstaller.Services
{
    public class CertificateDownloader : ICertificateDownloader
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

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
                    Console.WriteLine("Download canceled.");
                    return null;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    if (cancellationToken.IsCancellationRequested) return null;
                    Console.WriteLine("WARNING: attempt {0} failed for {1}: {2}", attempt, url, ex.Message);
                    await Task.Delay(delayBetweenAttempts.Value, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Unable to download certificates from {0} after {1} attempts.", url, attempt);
                    Console.WriteLine("ERROR: {0}", ex.Message);
                }
            }

            return null;
        }

        public async Task<X509Certificate2Collection> GetZIPCertificatesAsync(string url, CancellationToken cancellationToken = default)
        {
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            Console.WriteLine("Getting certificates from {0} please wait.", url);

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
                        Console.WriteLine("WARNING: Failed to load certificate from zip entry {0}: {1}", certificate.FullName, ex.Message);
                    }
                }
                Console.WriteLine("{0} certificates found.", certCollection.Count);
            }
            return certCollection;
        }

        public async Task<X509Certificate2Collection> GetP7BCertificatesAsync(string url, CancellationToken cancellationToken = default)
        {
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            Console.WriteLine("Getting certificates from {0} please wait.", url);

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

                Console.WriteLine("{0} certificates found.", certCollection.Count);
            }
            return certCollection;
        }
    }
}
