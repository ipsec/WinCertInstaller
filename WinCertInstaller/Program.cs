using System;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using WinCertInstaller.Models;
using WinCertInstaller.Configuration;
using WinCertInstaller.Services;

namespace WinCertInstaller
{
    public class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage: WinCertInstaller [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --iti        Install certificates from ITI");
            Console.WriteLine("  --mpf        Install certificates from MPF");
            Console.WriteLine("  --all        Install certificates from ITI and MPF (default)");
            Console.WriteLine("  --dry-run    Run without writing certificates to store");
            Console.WriteLine("  -q           Quiet mode (no pause at exit)");
            Console.WriteLine("  -h,--help    Show this help message");
            Console.WriteLine("Example: WinCertInstaller --iti --dry-run");
        }

        public static (CertSource source, bool dryRun, bool quiet, bool showHelp) ParseArguments(string[] args)
        {
            bool quiet = false;
            bool dryRun = false;
            CertSource selectedSources = CertSource.None;
            bool showHelp = false;

            if (args.Length == 0)
            {
                selectedSources = CertSource.All;
            }
            else
            {
                foreach (string arg in args)
                {
                    switch (arg.ToLowerInvariant())
                    {
                        case "-q":
                            quiet = true;
                            break;
                        case "--dry-run":
                            dryRun = true;
                            break;
                        case "--iti":
                            selectedSources |= CertSource.ITI;
                            break;
                        case "--mpf":
                            selectedSources |= CertSource.MPF;
                            break;
                        case "--all":
                            selectedSources = CertSource.All;
                            break;
                        case "-h":
                        case "--help":
                            showHelp = true;
                            break;
                        default:
                            throw new ArgumentException($"Wrong parameter: {arg}");
                    }
                }
            }

            if (selectedSources == CertSource.None && !showHelp)
            {
                throw new ArgumentException("No certificate source selected.");
            }

            return (selectedSources, dryRun, quiet, showHelp);
        }

        static void DisposeCertificates(X509Certificate2Collection collection)
        {
            if (collection == null) return;
            foreach (var cert in collection)
            {
                cert.Dispose();
            }
        }

        static async Task<int> Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("\nCanceling...");
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                var (selectedSources, dryRun, quiet, showHelp) = ParseArguments(args);

                if (showHelp)
                {
                    PrintUsage();
                    return 0;
                }

                if (dryRun)
                {
                    Console.WriteLine("Dry run mode enabled: no certificates will be added to the store.");
                }

                // Dependency Injection Composition Root
                ICertificateDownloader downloader = new CertificateDownloader();
                ICertificateValidator validator = new CertificateValidator();
                ICertificateInstaller installer = new CertificateInstaller(validator);

                if (selectedSources.HasFlag(CertSource.ITI))
                {
                    Console.WriteLine("====================== ITI ======================");
                    X509Certificate2Collection itiCertificates = await downloader.GetZIPCertificatesAsync(AppSettings.ITICertUrl, cts.Token);
                    if (itiCertificates.Count > 0)
                    {
                        installer.InstallCertificates(itiCertificates, dryRun);
                        DisposeCertificates(itiCertificates);
                    }
                }

                if (selectedSources.HasFlag(CertSource.MPF))
                {
                    Console.WriteLine("====================== MPF ======================");
                    X509Certificate2Collection mpfCertificates = await downloader.GetP7BCertificatesAsync(AppSettings.MPFCertUrl, cts.Token);
                    if (mpfCertificates.Count > 0)
                    {
                        installer.InstallCertificates(mpfCertificates, dryRun);
                        DisposeCertificates(mpfCertificates);
                    }
                }

                Console.WriteLine("=================================================");
                Console.WriteLine("Finished!");

                WaitForKeyPress(quiet);

                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                PrintUsage();
                WaitForKeyPress(quiet: false);
                return -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error: {0}", ex.Message);
                WaitForKeyPress(quiet: false);
                return -1;
            }
        }

        static void WaitForKeyPress(bool quiet)
        {
            if (!quiet)
            {
                Console.WriteLine();
                Console.WriteLine("To run without this prompt use -q parameter.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey(true);
            }
        }
    }
}
