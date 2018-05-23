using System;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;

namespace WinCertInstaller
{
    class Program
    {
        static Stream DownloadFile(String url)
        {
            WebClient client = new WebClient();
            Stream stream = null;
            try
            {
                stream = client.OpenRead(url);
            } catch (System.Net.WebException ex)
            {
                Console.WriteLine("ERROR: Unable to download certificates.");
                Console.WriteLine("ERROR: {0}", ex.Message);
            }

            return stream;
        }

        static X509Certificate2Collection GetITICertificates()
        {
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            String url = "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip";
            Console.WriteLine("ITI: Getting certificates from {0} please wait.", url);
            Stream stream = DownloadFile(url);

            if (stream != null) {
                ZipArchive archive = new ZipArchive(stream);

                foreach (ZipArchiveEntry certificate in archive.Entries)
                {
                    Stream certStrean = certificate.Open();
                    MemoryStream ms = new MemoryStream();
                    X509Certificate2 cert = new X509Certificate2();
                    certStrean.CopyTo(ms);
                    cert.Import(ms.ToArray());
                    certCollection.Add(cert);
                }
                Console.WriteLine("ITI: {0} certificates found.", certCollection.Count);
            }
            return certCollection;
        }

        static X509Certificate2Collection GetMPFCertificates()
        {
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            String url = "http://repositorio.acinterna.mpf.mp.br/ejbca/downloads/ACIMPF-cadeia-completa.p7b";
            Console.WriteLine("MPF: Getting certificates from {0} please wait.", url);
            Stream stream = DownloadFile(url);
            if (stream != null) { 
                MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                certCollection.Import(ms.ToArray());
                Console.WriteLine("MPF: {0} certificates found.", certCollection.Count);
            }
            return certCollection;
        }

        static void InstallCertificates(String name, X509Store store, X509Certificate2Collection certificates) {
            try
            {
                if (certificates.Count > 0)
                {
                    Console.WriteLine("{0}: Installing certificates.", name);
                    store.AddRange(certificates);
                    Console.WriteLine("{0}: Added {1} certificates to {2}.", name, certificates.Count, StoreName.Root);
                } else
                {
                    Console.WriteLine("{0}: No certificates to import.", name);
                }
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                System.Environment.Exit(-1);
            }

        }

        static void Main(string[] args)
        {
            bool quiet = false;
            if(args.Length > 0)
            {
                if (args[0] == "-q")
                {
                    quiet = true;
                } else
                {
                    Console.WriteLine("Wrong parameter. Aborting.");
                    Console.WriteLine("Valid parameters are only: -q");
                    System.Environment.Exit(-1);
                }
            }

            X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.MaxAllowed);

            Console.WriteLine("====================== ITI ======================");
            X509Certificate2Collection ITIcertificates = GetITICertificates();
            if (ITIcertificates.Count > 0) {
                InstallCertificates("ITI", store, ITIcertificates);
            }

            Console.WriteLine("====================== MPF ======================");
            X509Certificate2Collection MPFCertificates = GetMPFCertificates();
            if (MPFCertificates.Count > 0)
            {
                InstallCertificates("MPF", store, MPFCertificates);
            }
            
            store.Close();
            Console.WriteLine("=================================================");
            Console.WriteLine("Finished!");

            if (!quiet)
            {
                Console.WriteLine("To run in quiet mode use -q parameters.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey(true);
            }
            
        }
    }
}
