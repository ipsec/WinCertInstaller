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
        public string ITICertUrl { get; set; } = string.Empty;

        /// <summary>
        /// The URL to the P7B (PKCS #7) file containing MPF certificates.
        /// </summary>
        public string MPFCertUrl { get; set; } = string.Empty;
    }
}
