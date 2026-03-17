using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using WinCertInstaller.Models;
using WinCertInstaller.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace WinCertInstaller.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void ParseArguments_DefaultsToAll()
        {
            var result = WinCertInstaller.Program.ParseArguments(Array.Empty<string>());

            Assert.Equal(CertSource.All, result.source);
            Assert.False(result.dryRun);
            Assert.False(result.quiet);
            Assert.False(result.showHelp);
        }

        [Fact]
        public void ParseArguments_MpfAndDryRunAndQuiet_SetCorrectFlags()
        {
            var result = WinCertInstaller.Program.ParseArguments(new[] { "--mpf", "--dry-run", "-q" });

            Assert.Equal(CertSource.MPF, result.source);
            Assert.True(result.dryRun);
            Assert.True(result.quiet);
            Assert.False(result.showHelp);
        }

        [Fact]
        public void ParseArguments_HelpSet_ShowHelpTrue()
        {
            var result = WinCertInstaller.Program.ParseArguments(new[] { "--help" });

            Assert.Equal(CertSource.None, result.source);
            Assert.True(result.showHelp);
        }

        [Fact]
        public void ParseArguments_InvalidArgument_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => WinCertInstaller.Program.ParseArguments(new[] { "--unknown" }));
        }

        [Fact]
        public void IsCertificateAuthorityAndSelfSigned_TrueForSelfSignedCA()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=TestCA", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
            var validator = new CertificateValidator(NullLogger<CertificateValidator>.Instance);

            Assert.True(validator.IsCertificateAuthority(certificate));
            Assert.True(validator.IsSelfSigned(certificate));
        }
    }
}
