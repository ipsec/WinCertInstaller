using System.Text.Json.Serialization;
using WinCertInstaller.Configuration;

namespace WinCertInstaller.Configuration
{
    [JsonSerializable(typeof(AppSettings))]
    internal partial class AppSettingsJsonContext : JsonSerializerContext
    {
    }
}
