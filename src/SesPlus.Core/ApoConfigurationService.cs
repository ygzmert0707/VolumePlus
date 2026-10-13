using System.Globalization;
using System.Text;

namespace SesPlus.Core;

/// <summary>
/// Owns only a marked Include block and the canonical sesplus.txt it generates.
/// It does not install an APO or change Windows device registration.
/// </summary>
public sealed class ApoConfigurationService
{
    // Keep these legacy identifiers and generated-file bytes compatible with prior releases.
    // The Volume + brand does not create a second integration or orphan existing gain settings.
    private const string BeginMarker = "# SesPlus managed integration begin (v1)";
    private const string EndMarker = "# SesPlus managed integration end (v1)";
    private const string IncludeLine = "Include: sesplus.txt";
    private const string GeneratedHeader = "# SesPlus generated configuration v1";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly int[] ReplacementRetryDelays = [20, 40, 80, 160];
    private readonly object sync = new();
    private readonly string mainPath;
    private readonly string managedPath;

    public ApoConfigurationService(string configDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        if (!Path.IsPathFullyQualified(configDirectory))
            throw new ArgumentException("The Equalizer APO config directory must be an absolute path.", nameof(configDirectory));
        ConfigDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configDirectory));
        if (!Directory.Exists(ConfigDirectory))
            throw new DirectoryNotFoundException("The Equalizer APO config directory does not exist.");
        RejectLink(ConfigDirectory);
        mainPath = Path.Combine(ConfigDirectory, "config.txt");
        managedPath = Path.Combine(ConfigDirectory, "sesplus.txt");
    }

    public string ConfigDirectory { get; }
    public string? LastBackupPath { get; private set; }
    public bool IsIntegrated => ReadSettings() is not null;

    public BoosterSettings? ReadSettings()
    {
        lock (sync)
        {
            TextSnapshot main = ReadMain();
            ManagedBlock? block = FindBlock(main.Text);
            if (block is null || !File.Exists(managedPath))
                return null;
            return ParseManaged(ReadSafe(managedPath));
        }
    }

    /// <summary>
    /// Restores the currently integrated device to neutral gain without creating an integration.
    /// Configuration read or write errors propagate so callers can keep the application open.
    /// </summary>
    public BoosterSettings? ResetGain()
    {
        lock (sync)
        {
            BoosterSettings? current = ReadSettings();
            if (current is null)
                return null;
            BoosterSettings neutral = current with { Percent = 100, Enabled = false };
            Apply(neutral);
            return neutral;
        }
    }

    public void Apply(BoosterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _ = GainCalculator.ToDecibels(settings.Percent);
        _ = GainCalculator.ExtractDeviceGuid(settings.DeviceId);
        byte[] replacement = BuildManaged(settings);

        lock (sync)
        {
            // Read every time: the Equalizer APO editor and other EQ tools may edit this file.
            TextSnapshot main = ReadMain();
            ManagedBlock? block = FindBlock(main.Text);
            string originalText = block is null ? main.Text : main.Text.Remove(block.Start, block.Length);
            ValidateConditionalBalance(originalText);
            string newline = DetectNewline(originalText);
            // Include restores child Stage and Channel state; child EOF restores Device matching.
            // Prefixing avoids inheriting a previous Device or unclosed conditional scope.
            string integrated = BuildBlock(newline) + originalText;
            byte[] integratedBytes = main.Encode(integrated);
            byte[]? oldManaged = File.Exists(managedPath) ? ReadSafe(managedPath) : null;
            if (oldManaged is not null)
                _ = ParseManaged(oldManaged); // Refuse to overwrite an unrelated or manually edited file.

            bool managedChanged = oldManaged is null || !replacement.AsSpan().SequenceEqual(oldManaged);
            bool mainChanged = !integratedBytes.AsSpan().SequenceEqual(main.Bytes);
            if (!managedChanged && !mainChanged)
                return;

            if (mainChanged)
                Backup(main.Bytes);
            if (managedChanged)
                AtomicWrite(managedPath, replacement, oldManaged);
            try
            {
                if (mainChanged)
                    AtomicWrite(mainPath, integratedBytes, main.Bytes);
            }
            catch (Exception originalError)
            {
                if (managedChanged)
                {
                    try
                    {
                        // Never undo an external edit made during the failed transaction.
                        VerifyUnchanged(managedPath, replacement);
                        if (oldManaged is null)
                            File.Delete(managedPath);
                        else
                            AtomicWrite(managedPath, oldManaged, replacement);
                    }
                    catch (Exception rollbackError)
                    {
                        throw new IOException("The config update failed and sesplus.txt could not be restored. The existing main config was preserved.", new AggregateException(originalError, rollbackError));
                    }
                }
                throw;
            }
        }
    }

    public void RemoveIntegration()
    {
        lock (sync)
        {
            TextSnapshot main = ReadMain();
            ManagedBlock? block = FindBlock(main.Text);
            if (block is null)
                return;
            byte[]? managed = File.Exists(managedPath) ? ReadSafe(managedPath) : null;
            bool ownsManaged = managed is not null && TryParseManaged(managed, out _);
            byte[] removedBytes = main.Encode(main.Text.Remove(block.Start, block.Length));
            Backup(main.Bytes);
            // Remove the reference first, so it can never point to a file we just deleted.
            AtomicWrite(mainPath, removedBytes, main.Bytes);
            if (ownsManaged)
            {
                VerifyUnchanged(managedPath, managed);
                File.Delete(managedPath);
            }
        }
    }

    private TextSnapshot ReadMain()
    {
        if (!File.Exists(mainPath))
            throw new FileNotFoundException("The Equalizer APO config.txt file is missing.", mainPath);
        return TextSnapshot.Decode(ReadSafe(mainPath));
    }

    private static byte[] ReadSafe(string path)
    {
        RejectLink(path);
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("The configuration exceeds the supported 4 MB size limit.");
        return bytes;
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Symbolic links and reparse points are not supported for managed configuration files.");
    }

    private void Backup(byte[] bytes)
    {
        string path = Path.Combine(ConfigDirectory, $"config.txt.sesplus.{DateTime.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.bak");
        using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        LastBackupPath = path;
    }

    private static void VerifyUnchanged(string path, byte[]? expected)
    {
        if (expected is null)
        {
            if (File.Exists(path) || Directory.Exists(path))
                throw new IOException("Another application created the configuration file. Retry after checking it.");
        }
        else if (!File.Exists(path) || !ReadSafe(path).AsSpan().SequenceEqual(expected))
            throw new IOException("Another application changed the configuration file. Retry after checking it.");
    }

    private static void AtomicWrite(string path, byte[] bytes, byte[]? expected)
    {
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, $".sesplus-{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (expected is null)
            {
                VerifyUnchanged(path, expected);
                File.Move(temporary, path);
            }
            else
            {
                for (int attempt = 0; ; attempt++)
                {
                    // Recheck both files before each retry: another editor may have changed them
                    // while an audio engine or scanner briefly held the destination open.
                    if (attempt > 0)
                        VerifyUnchanged(temporary, bytes);
                    VerifyUnchanged(path, expected);
                    try
                    {
                        File.Replace(temporary, path, null);
                        break;
                    }
                    catch (IOException exception) when (attempt < ReplacementRetryDelays.Length && IsRetryableReplacementError(exception))
                    {
                        // Win32 1175 preserves both file names. 1176/1177 can move or remove
                        // the original and must not be retried as if the transaction were intact.
                        VerifyUnchanged(temporary, bytes);
                        VerifyUnchanged(path, expected);
                        Thread.Sleep(ReplacementRetryDelays[attempt]);
                    }
                }
            }
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static bool IsRetryableReplacementError(IOException exception) => exception.HResult is
        unchecked((int)0x80070020) or // ERROR_SHARING_VIOLATION (32)
        unchecked((int)0x80070021) or // ERROR_LOCK_VIOLATION (33)
        unchecked((int)0x80070497);   // ERROR_UNABLE_TO_REMOVE_REPLACED (1175)

    private static string BuildBlock(string newline) =>
        BeginMarker + newline + IncludeLine + newline + EndMarker + newline;

    private static ManagedBlock? FindBlock(string text)
    {
        List<Line> lines = GetLines(text);
        List<int> markerLines = [];
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Value.Contains("SesPlus managed integration", StringComparison.OrdinalIgnoreCase))
                markerLines.Add(i);
        }
        ManagedBlock? block = null;
        if (markerLines.Count != 0)
        {
            if (markerLines.Count != 2 || markerLines[1] != markerLines[0] + 2)
                throw new InvalidDataException("The Volume + integration markers are incomplete, duplicated or edited.");
            int begin = markerLines[0];
            int end = markerLines[1];
            if (lines[begin].Value != BeginMarker || lines[begin + 1].Value != IncludeLine || lines[end].Value != EndMarker)
                throw new InvalidDataException("The Volume + managed integration block was edited. Restore its original three lines before continuing.");
            block = new(lines[begin].Start, lines[end].End - lines[begin].Start);
        }
        foreach (Line line in lines)
        {
            if (block is not null && line.Start >= block.Start && line.Start < block.Start + block.Length)
                continue;
            if (ReferencesManagedFile(line.Value))
                throw new InvalidDataException("config.txt already references sesplus.txt outside the managed block. Remove that duplicate Include before continuing.");
        }
        return block;
    }

    private static bool ReferencesManagedFile(string line)
    {
        string trimmed = line.Trim();
        int colon = trimmed.IndexOf(':');
        if (colon < 0 || !trimmed[..colon].Trim().Equals("Include", StringComparison.OrdinalIgnoreCase))
            return false;
        string value = trimmed[(colon + 1)..].Trim().Trim('"').Replace('\\', '/');
        string name = value[(value.LastIndexOf('/') + 1)..];
        return name.Equals("sesplus.txt", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateConditionalBalance(string text)
    {
        int depth = 0;
        foreach (Line line in GetLines(text))
        {
            string value = line.Value.Trim();
            int colon = value.IndexOf(':');
            if (colon < 0)
                continue;
            string command = value[..colon].Trim();
            if (command == "If")
                depth++;
            else if (command == "EndIf")
            {
                if (--depth < 0)
                    throw new InvalidDataException("config.txt contains an EndIf without an If.");
            }
            else if ((command == "Else" || command == "ElseIf") && depth == 0)
                throw new InvalidDataException("config.txt contains an Else without an If.");
        }
        if (depth != 0)
            throw new InvalidDataException("config.txt contains an unclosed If. Close it before adding the booster.");
    }

    private static byte[] BuildManaged(BoosterSettings settings)
    {
        string guid = GainCalculator.ExtractDeviceGuid(settings.DeviceId);
        string gain = (settings.Enabled ? GainCalculator.ToDecibels(settings.Percent) : 0).ToString("0.######", CultureInfo.InvariantCulture);
        string id = Convert.ToBase64String(StrictUtf8.GetBytes(settings.DeviceId));
        string text = string.Join("\r\n",
            GeneratedHeader,
            "# Percent: " + settings.Percent.ToString(CultureInfo.InvariantCulture),
            "# Enabled: " + (settings.Enabled ? "true" : "false"),
            "# DeviceId: " + id,
            "# Managed by SesPlus. Use the app to change these settings.",
            "Device: " + guid,
            "Channel: all",
            "Stage: post-mix",
            "Preamp: " + gain + " dB", "");
        return StrictUtf8.GetBytes(text);
    }

    private static BoosterSettings ParseManaged(byte[] bytes)
    {
        if (!TryParseManaged(bytes, out BoosterSettings? settings))
            throw new InvalidDataException("sesplus.txt is unrelated to Volume + or has been manually edited. Back it up and rename it before applying settings.");
        return settings!;
    }

    private static bool TryParseManaged(byte[] bytes, out BoosterSettings? settings)
    {
        settings = null;
        try
        {
            string[] lines = StrictUtf8.GetString(bytes).Split("\r\n", StringSplitOptions.None);
            if (lines.Length != 10 || lines[0] != GeneratedHeader ||
                !lines[1].StartsWith("# Percent: ", StringComparison.Ordinal) ||
                !lines[2].StartsWith("# Enabled: ", StringComparison.Ordinal) ||
                !lines[3].StartsWith("# DeviceId: ", StringComparison.Ordinal))
                return false;
            if (!int.TryParse(lines[1][11..], NumberStyles.None, CultureInfo.InvariantCulture, out int percent))
                return false;
            string enabledText = lines[2][11..];
            if (enabledText is not ("true" or "false"))
                return false;
            string id = StrictUtf8.GetString(Convert.FromBase64String(lines[3][12..]));
            BoosterSettings parsed = new(percent, enabledText == "true", id);
            _ = GainCalculator.ToDecibels(percent);
            if (!BuildManaged(parsed).AsSpan().SequenceEqual(bytes))
                return false;
            settings = parsed;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return false;
        }
    }

    private static string DetectNewline(string text)
    {
        int index = text.IndexOfAny(['\r', '\n']);
        if (index < 0)
            return "\r\n";
        return text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : text[index].ToString();
    }

    private static List<Line> GetLines(string text)
    {
        List<Line> lines = [];
        int start = 0;
        while (start < text.Length)
        {
            int end = start;
            while (end < text.Length && text[end] is not ('\r' or '\n'))
                end++;
            int contentEnd = end;
            if (end < text.Length && text[end++] == '\r' && end < text.Length && text[end] == '\n')
                end++;
            lines.Add(new(start, end, text[start..contentEnd]));
            start = end;
        }
        return lines;
    }

    private sealed record ManagedBlock(int Start, int Length);
    private sealed record Line(int Start, int End, string Value);

    private sealed record TextSnapshot(byte[] Bytes, string Text, Encoding Encoding, byte[] Preamble)
    {
        public byte[] Encode(string text) => [.. Preamble, .. Encoding.GetBytes(text)];

        public static TextSnapshot Decode(byte[] bytes)
        {
            (Encoding Encoding, int Prefix) selected = bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 }) ? (new UTF32Encoding(false, false, true), 4)
                : bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xFE, 0xFF }) ? (new UTF32Encoding(true, false, true), 4)
                : bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }) ? (new UnicodeEncoding(false, false, true), 2)
                : bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }) ? (new UnicodeEncoding(true, false, true), 2)
                : bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? (StrictUtf8, 3)
                : (StrictUtf8, 0);
            (Encoding encoding, int prefix) = selected;
            try
            {
                return new(bytes, encoding.GetString(bytes, prefix, bytes.Length - prefix), encoding, bytes[..prefix]);
            }
            catch (DecoderFallbackException) when (prefix == 0)
            {
                // ASCII markers can be inserted while preserving every legacy code-page byte.
                return new(bytes, Encoding.Latin1.GetString(bytes), Encoding.Latin1, []);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("config.txt has an invalid byte sequence for its declared encoding.", exception);
            }
        }
    }
}
