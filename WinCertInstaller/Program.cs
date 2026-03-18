using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using WinCertInstaller.Models;
using WinCertInstaller.Configuration;
using WinCertInstaller.Services;
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

                // --- Lightweight Initialization ---
                
                // 1. Load Configuration
                AppSettings settings = LoadSettings();
                
                // 2. Initialize Loggers
                var programLogger = new SimpleLogger<Program>();
                var downloaderLogger = new SimpleLogger<CertificateDownloader>();
                var validatorLogger = new SimpleLogger<CertificateValidator>();
                var installerLogger = new SimpleLogger<CertificateInstaller>();

                // 3. Initialize Services
                var validator = new CertificateValidator(validatorLogger);
                var downloader = new CertificateDownloader(downloaderLogger);
                var installer = new CertificateInstaller(validator, installerLogger);

                if (selectedSources.HasFlag(CertSource.ITI))
                {
                    programLogger.LogInformation("====================== ITI ======================");
                    if (string.IsNullOrWhiteSpace(settings.ITICertUrl))
                    {
                        programLogger.LogError("Configuration Error: ITICertUrl is empty or missing from appsettings.json.");
                    }
                    else
                    {
                        X509Certificate2Collection itiCertificates = await downloader.GetZIPCertificatesAsync(settings.ITICertUrl, cts.Token);
                        if (itiCertificates.Count > 0)
                        {
                            installer.InstallCertificates(itiCertificates, dryRun);
                            DisposeCertificates(itiCertificates);
                        }
                    }
                }

                if (selectedSources.HasFlag(CertSource.MPF))
                {
                    programLogger.LogInformation("====================== MPF ======================");
                    if (string.IsNullOrWhiteSpace(settings.MPFCertUrl))
                    {
                        programLogger.LogError("Configuration Error: MPFCertUrl is empty or missing from appsettings.json.");
                    }
                    else
                    {
                        X509Certificate2Collection mpfCertificates = await downloader.GetP7BCertificatesAsync(settings.MPFCertUrl, cts.Token);
                        if (mpfCertificates.Count > 0)
                        {
                            installer.InstallCertificates(mpfCertificates, dryRun);
                            DisposeCertificates(mpfCertificates);
                        }
                    }
                }

                programLogger.LogInformation("=================================================");
                programLogger.LogInformation("Installation process completed.");

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

        private static AppSettings LoadSettings()
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(configPath);
                if (string.IsNullOrWhiteSpace(json)) return new AppSettings();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                // Try to get the "CertificateSources" property if it exists (nested structure support)
                if (root.TryGetProperty("CertificateSources", out var sources))
                {
                    return JsonSerializer.Deserialize(sources.GetRawText(), AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
                }
                
                // Try to deserialize from the root (flat structure support)
                return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
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
