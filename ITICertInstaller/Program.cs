using System;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;

namespace ITICertInstaller
{
    class Program
    {
        static void Main(string[] args)
        {
            String url = "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip";
            WebClient client = new WebClient();
            Stream zipCertificateStream = client.OpenRead(url);
            ZipArchive archive = new ZipArchive(zipCertificateStream);

            X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.MaxAllowed);


            foreach (ZipArchiveEntry certificate in archive.Entries)
            {
                Stream certStrean = certificate.Open();
                X509Certificate2 cert = new X509Certificate2();
                MemoryStream ms = new MemoryStream();
                certStrean.CopyTo(ms);
                cert.Import(ms.ToArray());
                Console.WriteLine(cert.Subject);
                try
                {
                    store.Add(cert);
                } catch (System.Security.Cryptography.CryptographicException ex)
                {
                    Console.WriteLine(ex.Message);
                    System.Environment.Exit(-1);
                }
            }
            store.Close();
        }
    }
}
