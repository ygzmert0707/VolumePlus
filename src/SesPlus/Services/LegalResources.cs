using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SesPlus.Services;

internal static class LegalResources
{
    public static bool RequiresRuntimeAgreement => typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Any(attribute => attribute.Key == "BundledMicrosoftRuntime" && string.Equals(attribute.Value, "true", StringComparison.OrdinalIgnoreCase));

    public static string Read(string path)
    {
        var normalized = "SesPlus.Legal." + path.Replace('\\', '/');
        var assembly = typeof(App).Assembly;
        var name = assembly.GetManifestResourceNames().SingleOrDefault(name => name.Replace('\\', '/').Equals(normalized, StringComparison.Ordinal));
        if (name is null) throw new InvalidOperationException("A required bundled license is missing: " + path);
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static string RuntimeAgreementHash
    {
        get
        {
            var texts = Read("microsoft/DOTNET-LIBRARY-LICENSE.txt") + "\n" + Read("microsoft/WINDOWS-SDK-LICENSE.txt");
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texts)));
        }
    }

    public static string Notices => string.Join("\n\n", new[]
    {
        "=== .NET runtime MIT license ===\n" + Read("dotnet/LICENSE.txt"),
        "=== .NET runtime third-party notices ===\n" + Read("dotnet/THIRD-PARTY-NOTICES.txt"),
        "=== WPF MIT license ===\n" + Read("wpf/LICENSE.txt"),
        "=== WPF third-party notices ===\n" + Read("wpf/THIRD-PARTY-NOTICES.txt"),
        "=== Windows Forms MIT license ===\n" + Read("winforms/LICENSE.txt"),
        "=== Windows Forms third-party notices ===\n" + Read("winforms/THIRD-PARTY-NOTICES.txt")
    });
}
