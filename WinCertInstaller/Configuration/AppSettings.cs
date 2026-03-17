namespace WinCertInstaller.Configuration
{
    /// <summary>
    /// Application settings containing default URLs for certificate downloads.
    /// </summary>
    public static class AppSettings
    {
        /// <summary>
        /// The URL to the ZIP file containing ITI (ICP-Brasil) certificates.
        /// </summary>
        public const string ITICertUrl = "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip";

        /// <summary>
        /// The URL to the P7B (PKCS #7) file containing MPF certificates.
        /// </summary>
        public const string MPFCertUrl = "http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.p7b";
    }
}
