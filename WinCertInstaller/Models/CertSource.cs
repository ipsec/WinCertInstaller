using System;

namespace WinCertInstaller.Models
{
    /// <summary>
    /// Represents the sources from which certificates should be installed.
    /// This enumeration supports bitwise combinations (Flags).
    /// </summary>
    [Flags]
    public enum CertSource
    {
        /// <summary>No source selected.</summary>
        None = 0,
        
        /// <summary>Installs certificates from ITI (Instituto Nacional de Tecnologia da Informação).</summary>
        ITI = 1,
        
        /// <summary>Installs certificates from MPF (Ministério Público Federal).</summary>
        MPF = 2,
        
        /// <summary>Installs certificates from all available sources.</summary>
        All = ITI | MPF
    }
}
