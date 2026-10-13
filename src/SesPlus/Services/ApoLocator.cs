using Microsoft.Win32;
using System.IO;

namespace SesPlus.Services;

public sealed record ApoInstallation(string InstallDirectory, string ConfigDirectory, string? SelectorPath);

public static class ApoLocator
{
    public const string OfficialDownloadUrl = "https://sourceforge.net/projects/equalizerapo/";

    public static ApoInstallation? Find()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = hive.OpenSubKey(@"SOFTWARE\EqualizerAPO");
                if (key is null) continue;
                var install = key.GetValue("InstallPath") as string;
                var config = key.GetValue("ConfigPath") as string;
                if (!string.IsNullOrWhiteSpace(config) && File.Exists(Path.Combine(config, "config.txt")))
                    return MakeInstallation(install ?? Directory.GetParent(config)?.FullName ?? config, config);
                if (!string.IsNullOrWhiteSpace(install) && TryDirectory(install) is { } found) return found;
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        }
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            if (!string.IsNullOrEmpty(root) && TryDirectory(Path.Combine(root, "EqualizerAPO")) is { } found) return found;
        return null;
    }

    private static ApoInstallation? TryDirectory(string install)
    {
        var config = Path.Combine(install, "config");
        return File.Exists(Path.Combine(config, "config.txt")) ? MakeInstallation(install, config) : null;
    }

    private static ApoInstallation MakeInstallation(string install, string config)
    {
        var selector = new[] { "DeviceSelector.exe", "Configurator.exe" }.Select(name => Path.Combine(install, name)).FirstOrDefault(File.Exists);
        return new(install, config, selector);
    }
}
