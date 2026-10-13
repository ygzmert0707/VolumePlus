using System.IO;
using System.Text.Json;

namespace SesPlus.Services;

public sealed record Preferences(string Language = "en", int Percent = 100, string? DeviceId = null, string? RuntimeLicenseAcceptance = null);

public static class PreferenceStore
{
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SesPlus", "preferences.json");

    public static Preferences Load()
    {
        try
        {
            var saved = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath));
            return saved is null ? new() : saved with { Language = Localization.NormalizeLanguage(saved.Language), Percent = saved.Percent >= 1 ? saved.Percent : 100 };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public static void Save(Preferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences));
        File.Move(temporaryPath, FilePath, true);
    }
}
