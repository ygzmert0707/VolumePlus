using System.Text.RegularExpressions;

namespace SesPlus.Core;

public static partial class GainCalculator
{
    public const int MinimumPercent = 1;
    public const int StandardMaximumPercent = 200;

    /// <summary>Converts an amplitude percentage to additive gain in decibels.</summary>
    public static double ToDecibels(int percent)
    {
        if (percent < MinimumPercent)
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "Amplitude percentage must be positive.");
        return 20 * Math.Log10(percent / 100.0);
    }

    /// <summary>Returns the endpoint GUID from a complete Windows playback MMDevice ID.</summary>
    public static string ExtractDeviceGuid(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        Match match = PlaybackDeviceId().Match(deviceId);
        if (!match.Success || !Guid.TryParseExact(match.Groups[1].Value, "B", out Guid guid) || guid == Guid.Empty)
            throw new ArgumentException("A complete Windows playback device ID is required.", nameof(deviceId));
        return guid.ToString("B");
    }

    [GeneratedRegex(@"\A\{0\.0\.0\.00000000\}\.(\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\})\z", RegexOptions.CultureInvariant)]
    private static partial Regex PlaybackDeviceId();
}
