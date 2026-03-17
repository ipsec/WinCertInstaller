using System;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using WinCertInstaller.Models;
using WinCertInstaller.Configuration;
using WinCertInstaller.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using WinCertInstaller.Logging;

namespace WinCertInstaller
{
    public class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage: WinCertInstaller [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --iti        Install ITI certificates");
            Console.WriteLine("  --mpf        Install MPF certificates");
            Console.WriteLine("  --all        Install both ITI and MPF certificates (default)");
            Console.WriteLine("  --dry-run    Simulate installation without writing to the store");
            Console.WriteLine("  -q           Quiet mode (suppress exit prompt)");
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
                    Console.WriteLine("Dry run enabled: No changes will be made to the certificate store.");
                }

                var host = Host.CreateDefaultBuilder(args)
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole(options => options.FormatterName = "CleanLayout")
                               .AddConsoleFormatter<CleanConsoleFormatter, ConsoleFormatterOptions>();
                    })
                    .ConfigureServices((context, services) =>
                    {
                        services.Configure<AppSettings>(context.Configuration.GetSection(AppSettings.Position));
                        services.AddTransient<ICertificateDownloader, CertificateDownloader>();
                        services.AddTransient<ICertificateValidator, CertificateValidator>();
                        services.AddTransient<ICertificateInstaller, CertificateInstaller>();
                    })
                    .Build();

                var downloader = host.Services.GetRequiredService<ICertificateDownloader>();
                var validator = host.Services.GetRequiredService<ICertificateValidator>();
                var installer = host.Services.GetRequiredService<ICertificateInstaller>();
                var appSettings = host.Services.GetRequiredService<IOptions<AppSettings>>().Value;
                var logger = host.Services.GetRequiredService<ILogger<Program>>();

                if (selectedSources.HasFlag(CertSource.ITI))
                {
                    logger.LogInformation("====================== ITI ======================");
                    X509Certificate2Collection itiCertificates = await downloader.GetZIPCertificatesAsync(appSettings.ITICertUrl, cts.Token);
                    if (itiCertificates.Count > 0)
                    {
                        installer.InstallCertificates(itiCertificates, dryRun);
                        DisposeCertificates(itiCertificates);
                    }
                }

                if (selectedSources.HasFlag(CertSource.MPF))
                {
                    logger.LogInformation("====================== MPF ======================");
                    X509Certificate2Collection mpfCertificates = await downloader.GetP7BCertificatesAsync(appSettings.MPFCertUrl, cts.Token);
                    if (mpfCertificates.Count > 0)
                    {
                        installer.InstallCertificates(mpfCertificates, dryRun);
                        DisposeCertificates(mpfCertificates);
                    }
                }

                logger.LogInformation("=================================================");
                logger.LogInformation("Installation process completed.");

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
                Console.WriteLine("Use the -q parameter to run without this prompt.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
        }
    }
}
