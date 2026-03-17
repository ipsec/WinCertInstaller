using System;

namespace WinCertInstaller.Models
{
    [Flags]
    public enum CertSource
    {
        None = 0,
        ITI = 1,
        MPF = 2,
        All = ITI | MPF
    }
}
