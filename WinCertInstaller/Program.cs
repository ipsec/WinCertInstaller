using System;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Linq;

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

        static X509Certificate2Collection GetZIPCertificates(String url)
        {
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            Console.WriteLine("Getting certificates from {0} please wait.", url);
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
                Console.WriteLine("{0} certificates found.", certCollection.Count);
            }
            return certCollection;
        }

        static X509Certificate2Collection GetP7BCertificates(String url)
        {
            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            Console.WriteLine("Getting certificates from {0} please wait.", url);
            Stream stream = DownloadFile(url);
            if (stream != null) { 
                MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                certCollection.Import(ms.ToArray());
                Console.WriteLine("{0} certificates found.", certCollection.Count);
            }
            return certCollection;
        }

        static void Add(X509Certificate2Collection certificates, StoreName storeName, StoreLocation location)
        {
            X509Store store = new X509Store(storeName, location);
            store.Open(OpenFlags.MaxAllowed);
            Console.WriteLine("Installing certificates.");
            store.AddRange(certificates);
            Console.WriteLine("Added {0} certificates to {1}.", certificates.Count, storeName);
            store.Close();
        }

        static void InstallCertificates(X509Certificate2Collection certificates) {
            X509Certificate2Collection CACertificates = new X509Certificate2Collection();
            X509Certificate2Collection CAIntermediateCertificates = new X509Certificate2Collection();

            foreach (X509Certificate2 cert in certificates)
            {
                bool isCA = IsCertificateAuthority(cert);
                bool isSelfSigned = IsSelfSigned(cert);
                if (isCA)
                {
                    if (isSelfSigned)
                    {
                        CACertificates.Add(cert);
                    }
                    else
                    {
                        CAIntermediateCertificates.Add(cert);
                    }
                }
                else
                {
                    Console.WriteLine("{0} is not a CA. Ignoring.", cert.Subject);
                }
            }

            try
            {
                if (CACertificates.Count > 0)
                {
                    Add(CACertificates, StoreName.Root, StoreLocation.LocalMachine);
                } else
                {
                    Console.WriteLine("No CA certificates to import.");
                }

                if (CAIntermediateCertificates.Count > 0)
                {
                    Add(CAIntermediateCertificates, StoreName.CertificateAuthority, StoreLocation.LocalMachine);
                }
                else
                {
                    Console.WriteLine("No Intermediate CA certificates to import.");
                }
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                System.Environment.Exit(-1);
            }

        }

        public static bool IsCertificateAuthority(X509Certificate2 certificate)
        {           
            foreach (X509BasicConstraintsExtension basic_constraints in certificate.Extensions.OfType<X509BasicConstraintsExtension>())
            {
                if (basic_constraints.CertificateAuthority)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsSelfSigned(X509Certificate2 certificate)
        {
            if (certificate.Issuer == certificate.Subject)
            {
                return true;
            }
            return false;
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

            Console.WriteLine("====================== ITI ======================");
            X509Certificate2Collection ITIcertificates = GetZIPCertificates("http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip");
            if (ITIcertificates.Count > 0) {
                InstallCertificates(ITIcertificates);
            }

            Console.WriteLine("====================== MPF ======================");
            X509Certificate2Collection MPFCertificates = GetP7BCertificates("http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.p7b");

            if (MPFCertificates.Count > 0)
            {
                InstallCertificates(MPFCertificates);
            }
            
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
