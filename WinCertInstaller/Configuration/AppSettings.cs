namespace WinCertInstaller.Configuration
{
    /// <summary>
    /// Application settings containing default URLs for certificate downloads.
    /// Mapped directly from appsettings.json via IOptions.
    /// </summary>
    using System.Diagnostics.CodeAnalysis;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class AppSettings
    {
        public const string Position = "CertificateSources";

        /// <summary>
        /// The URL to the ZIP file containing ITI (ICP-Brasil) certificates.
        /// </summary>
        public string ITICertUrl { get; set; } = "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip";

        /// <summary>
        /// The URL to the P7B (PKCS #7) file containing MPF certificates.
        /// </summary>
        public string MPFCertUrl { get; set; } = "http://repositorio.acinterna.mpf.mp.br/ejbca/ra/downloads/ACIMPF-cadeia-completa.p7b";
    }
}
