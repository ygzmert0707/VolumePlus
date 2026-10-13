using System.Globalization;
using System.Text;
using System.Text.Json;
using SesPlus.Core;

const string DeviceId = "{0.0.0.00000000}.{11111111-2222-3333-4444-555555555555}";
int failed = 0;
int passed = 0;
List<(string Name, Action Test)> tests =
[
    ("100% is neutral; 200% doubles amplitude", () =>
    {
        Equal(0.0, GainCalculator.ToDecibels(100));
        Near(6.020599913279624, GainCalculator.ToDecibels(200), 1e-12);
        Near(1.5, Math.Pow(10, GainCalculator.ToDecibels(150) / 20), 1e-12);
    }),
    ("Gain is monotonic throughout the standard slider range", () =>
    {
        Equal(1, GainCalculator.MinimumPercent);
        Equal(200, GainCalculator.StandardMaximumPercent);
        for (int percent = 101; percent <= GainCalculator.StandardMaximumPercent; percent++)
            True(GainCalculator.ToDecibels(percent) > GainCalculator.ToDecibels(percent - 1));
    }),
    ("Nonpositive percentages are rejected", () =>
    {
        foreach (int percent in new[] { int.MinValue, -1, 0 })
            Throws<ArgumentOutOfRangeException>(() => GainCalculator.ToDecibels(percent));
    }),
    ("Custom percentages above 200 retain their requested amplitude", () =>
    {
        foreach (int percent in new[] { 201, 350, 500 })
            Near(percent / 100.0, Math.Pow(10, GainCalculator.ToDecibels(percent) / 20), 1e-12);
    }),
    ("Positive percentages below 100 produce attenuation", () =>
    {
        Near(-40, GainCalculator.ToDecibels(1), 1e-12);
        Near(-2.4987747321659985, GainCalculator.ToDecibels(75), 1e-12);
        True(GainCalculator.ToDecibels(99) < 0);
    }),
    ("Only complete playback device IDs are accepted", () =>
    {
        Equal("{11111111-2222-3333-4444-555555555555}", GainCalculator.ExtractDeviceGuid(DeviceId));
        foreach (string id in new[] { "", "all", "{11111111-2222-3333-4444-555555555555}",
            DeviceId + "\nPreamp: 100 dB", " " + DeviceId, DeviceId + " ",
            "{0.0.1.00000000}.{11111111-2222-3333-4444-555555555555}",
            "{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}" })
            Throws<ArgumentException>(() => GainCalculator.ExtractDeviceGuid(id));
        Throws<ArgumentNullException>(() => GainCalculator.ExtractDeviceGuid(null!));
    }),
    ("Config directory must be absolute and exist", () =>
    {
        Throws<ArgumentException>(() => new ApoConfigurationService("relative"));
        Throws<DirectoryNotFoundException>(() => new ApoConfigurationService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }),
    ("Missing config.txt is never silently created", () =>
    {
        using Sandbox box = new();
        File.Delete(box.Main);
        Throws<FileNotFoundException>(() => box.Service.Apply(new(200, true, DeviceId)));
        True(!File.Exists(box.Managed));
    }),
    ("First integration preserves existing scoped EQ and backups exact bytes", () =>
    {
        using Sandbox box = new("# user settings\r\nDevice: HDMI\r\nChannel: L\r\nStage: pre-mix\r\nPreamp: -4 dB");
        byte[] original = File.ReadAllBytes(box.Main);
        box.Service.Apply(new(175, true, DeviceId));
        string modified = File.ReadAllText(box.Main);
        True(modified.StartsWith("# SesPlus managed integration begin (v1)\r\nInclude: sesplus.txt\r\n# SesPlus managed integration end (v1)\r\n", StringComparison.Ordinal));
        True(modified.EndsWith(Encoding.UTF8.GetString(original), StringComparison.Ordinal));
        Bytes(original, File.ReadAllBytes(box.Service.LastBackupPath!));
        True(box.Service.IsIntegrated);
        Equal(new BoosterSettings(175, true, DeviceId), box.Service.ReadSettings());
        string managed = File.ReadAllText(box.Managed);
        True(managed.Contains("Device: {11111111-2222-3333-4444-555555555555}\r\nChannel: all\r\nStage: post-mix\r\n", StringComparison.Ordinal));
    }),
    ("Apply and remove preserve UTF8 without BOM exactly", () => Preservation(new UTF8Encoding(false), "# Türkçe 🎧\nDevice: Speakers\nPreamp: -3 dB")),
    ("Apply and remove preserve UTF8 BOM exactly", () => Preservation(new UTF8Encoding(true), "# Türkçe 🎧\r\nPreamp: -3 dB\r\n")),
    ("Apply and remove preserve UTF16 little endian BOM exactly", () => Preservation(new UnicodeEncoding(false, true), "# Türkçe 🎧\r\nChannel: R")),
    ("Apply and remove preserve UTF16 big endian BOM exactly", () => Preservation(new UnicodeEncoding(true, true), "# Türkçe 🎧\nChannel: R\n")),
    ("Apply and remove preserve UTF32 BOM exactly", () => Preservation(new UTF32Encoding(false, true), "# Türkçe 🎧\r\nChannel: R")),
    ("Apply and remove preserve legacy ANSI bytes exactly", () =>
    {
        using Sandbox box = new();
        byte[] original = [0x23, 0x20, 0xFD, 0xFE, 0x0D, 0x0A, 0x23, 0x20, 0xC0];
        File.WriteAllBytes(box.Main, original);
        box.Service.Apply(new(150, true, DeviceId));
        box.Service.RemoveIntegration();
        Bytes(original, File.ReadAllBytes(box.Main));
    }),
    ("Empty config round trips to empty bytes", () =>
    {
        using Sandbox box = new("");
        box.Service.Apply(new(200, true, DeviceId));
        box.Service.RemoveIntegration();
        Equal(0, File.ReadAllBytes(box.Main).Length);
    }),
    ("Applying identical settings does not rewrite or create a second backup", () =>
    {
        using Sandbox box = new();
        BoosterSettings settings = new(200, true, DeviceId);
        box.Service.Apply(settings);
        byte[] main = File.ReadAllBytes(box.Main);
        byte[] managed = File.ReadAllBytes(box.Managed);
        DateTime mainWrite = File.GetLastWriteTimeUtc(box.Main);
        DateTime managedWrite = File.GetLastWriteTimeUtc(box.Managed);
        box.Service.Apply(settings);
        Bytes(main, File.ReadAllBytes(box.Main));
        Bytes(managed, File.ReadAllBytes(box.Managed));
        Equal(mainWrite, File.GetLastWriteTimeUtc(box.Main));
        Equal(managedWrite, File.GetLastWriteTimeUtc(box.Managed));
        Equal(1, Directory.GetFiles(box.Directory, "*.bak").Length);
    }),
    ("Changing gain rewrites only the sibling file and persists across instances", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(110, true, DeviceId));
        byte[] main = File.ReadAllBytes(box.Main);
        box.Service.Apply(new(180, true, DeviceId));
        Bytes(main, File.ReadAllBytes(box.Main));
        Equal(new BoosterSettings(180, true, DeviceId), new ApoConfigurationService(box.Directory).ReadSettings());
        Equal(1, Directory.GetFiles(box.Directory, "*.bak").Length);
    }),
    ("Custom 350 and 500 percent gains round trip through canonical configuration", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(350, true, DeviceId));
        byte[] main = File.ReadAllBytes(box.Main);
        foreach (int percent in new[] { 350, 500 })
        {
            box.Service.Apply(new(percent, true, DeviceId));
            Equal(new BoosterSettings(percent, true, DeviceId), new ApoConfigurationService(box.Directory).ReadSettings());
            string gain = GainCalculator.ToDecibels(percent).ToString("0.######", CultureInfo.InvariantCulture);
            True(File.ReadAllText(box.Managed).Contains("Preamp: " + gain + " dB\r\n", StringComparison.Ordinal));
            Bytes(main, File.ReadAllBytes(box.Main));
            Equal(1, Directory.GetFiles(box.Directory, "*.bak").Length);
        }
    }),
    ("Enabled 75 percent configuration contains its negative gain", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(75, true, DeviceId));
        Equal(new BoosterSettings(75, true, DeviceId), box.Service.ReadSettings());
        True(File.ReadAllText(box.Managed).Contains("Preamp: -2.498775 dB\r\n", StringComparison.Ordinal));
    }),
    ("Largest integer percentage remains finite and round trips JSON and canonical configuration", () =>
    {
        BoosterSettings requested = new(int.MaxValue, true, DeviceId);
        double gain = GainCalculator.ToDecibels(requested.Percent);
        True(double.IsFinite(gain));
        Near(int.MaxValue / 100.0, Math.Pow(10, gain / 20), 1e-7);
        BoosterSettings decoded = JsonSerializer.Deserialize<BoosterSettings>(JsonSerializer.Serialize(requested))!;
        Equal(requested, decoded);
        using Sandbox box = new();
        box.Service.Apply(decoded);
        Equal(requested, new ApoConfigurationService(box.Directory).ReadSettings());
        True(File.ReadAllText(box.Managed).Contains("# Percent: 2147483647\r\n", StringComparison.Ordinal));
        string expectedGain = gain.ToString("0.######", CultureInfo.InvariantCulture);
        True(File.ReadAllText(box.Managed).Contains("Preamp: " + expectedGain + " dB\r\n", StringComparison.Ordinal));
        byte[] main = File.ReadAllBytes(box.Main);
        byte[] managed = File.ReadAllBytes(box.Managed);
        DateTime managedWrite = File.GetLastWriteTimeUtc(box.Managed);
        box.Service.Apply(decoded);
        Bytes(main, File.ReadAllBytes(box.Main));
        Bytes(managed, File.ReadAllBytes(box.Managed));
        Equal(managedWrite, File.GetLastWriteTimeUtc(box.Managed));
        Equal(1, Directory.GetFiles(box.Directory, "*.bak").Length);
    }),
    ("Disabling adds zero gain and retains the chosen percentage", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(200, false, DeviceId));
        True(File.ReadAllText(box.Managed).Contains("Preamp: 0 dB\r\n", StringComparison.Ordinal));
        Equal(new BoosterSettings(200, false, DeviceId), box.Service.ReadSettings());
    }),
    ("Reset restores 200 percent to neutral and preserves the existing EQ and backup", () =>
    {
        const string original = "# user EQ\r\nDevice: HDMI\r\nChannel: R\r\nStage: pre-mix\r\nPreamp: -4 dB\r\nFilter: ON PK Fc 100 Hz Gain 8 dB Q 1\r\n";
        using Sandbox box = new(original);
        byte[] originalBytes = File.ReadAllBytes(box.Main);
        box.Service.Apply(new(200, true, DeviceId));
        byte[] integratedMain = File.ReadAllBytes(box.Main);
        string backupPath = box.Service.LastBackupPath!;

        Equal(new BoosterSettings(100, false, DeviceId), box.Service.ResetGain());

        Equal(new BoosterSettings(100, false, DeviceId), new ApoConfigurationService(box.Directory).ReadSettings());
        True(File.ReadAllText(box.Managed).Contains("Preamp: 0 dB\r\n", StringComparison.Ordinal));
        Bytes(integratedMain, File.ReadAllBytes(box.Main));
        True(File.ReadAllText(box.Main).EndsWith(original, StringComparison.Ordinal));
        Bytes(originalBytes, File.ReadAllBytes(backupPath));
        Equal(backupPath, box.Service.LastBackupPath);
        Equal(1, Directory.GetFiles(box.Directory, "*.bak").Length);
    }),
    ("Reset restores a custom gain to disabled 100 percent while preserving user EQ", () =>
    {
        const string original = "# user EQ\r\nDevice: HDMI\r\nPreamp: -4 dB\r\nFilter: ON PK Fc 500 Hz Gain -2 dB Q 1\r\n";
        foreach (int percent in new[] { 75, 350, 500, int.MaxValue })
        {
            using Sandbox box = new(original);
            byte[] originalBytes = File.ReadAllBytes(box.Main);
            box.Service.Apply(new(percent, true, DeviceId));
            byte[] main = File.ReadAllBytes(box.Main);
            string backupPath = box.Service.LastBackupPath!;

            Equal(new BoosterSettings(100, false, DeviceId), box.Service.ResetGain());

            Equal(new BoosterSettings(100, false, DeviceId), new ApoConfigurationService(box.Directory).ReadSettings());
            True(File.ReadAllText(box.Managed).Contains("Preamp: 0 dB\r\n", StringComparison.Ordinal));
            Bytes(main, File.ReadAllBytes(box.Main));
            True(File.ReadAllText(box.Main).EndsWith(original, StringComparison.Ordinal));
            Bytes(originalBytes, File.ReadAllBytes(backupPath));
            Equal(backupPath, box.Service.LastBackupPath);
            Equal(1, Directory.GetFiles(box.Directory, "*.bak").Length);
        }
    }),
    ("Repeated reset does not rewrite files or create backups", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(200, true, DeviceId));
        box.Service.ResetGain();
        byte[] main = File.ReadAllBytes(box.Main);
        byte[] managed = File.ReadAllBytes(box.Managed);
        DateTime mainWrite = File.GetLastWriteTimeUtc(box.Main);
        DateTime managedWrite = File.GetLastWriteTimeUtc(box.Managed);
        string[] files = Directory.GetFiles(box.Directory).Order().ToArray();

        Equal(new BoosterSettings(100, false, DeviceId), box.Service.ResetGain());

        Bytes(main, File.ReadAllBytes(box.Main));
        Bytes(managed, File.ReadAllBytes(box.Managed));
        Equal(mainWrite, File.GetLastWriteTimeUtc(box.Main));
        Equal(managedWrite, File.GetLastWriteTimeUtc(box.Managed));
        True(files.SequenceEqual(Directory.GetFiles(box.Directory).Order()));
        Equal(1, Directory.GetFiles(box.Directory, "*.bak").Length);
    }),
    ("Reset without integration does not create or modify any configuration", () =>
    {
        using Sandbox box = new();
        byte[] main = File.ReadAllBytes(box.Main);
        DateTime mainWrite = File.GetLastWriteTimeUtc(box.Main);

        True(box.Service.ResetGain() is null);

        Bytes(main, File.ReadAllBytes(box.Main));
        Equal(mainWrite, File.GetLastWriteTimeUtc(box.Main));
        True(!File.Exists(box.Managed));
        Equal(1, Directory.GetFiles(box.Directory).Length);
        True(box.Service.LastBackupPath is null);
    }),
    ("Reset without an integration preserves an unrelated sibling file", () =>
    {
        using Sandbox box = new();
        File.WriteAllText(box.Managed, "# user's settings\nPreamp: -12 dB\n");
        byte[] main = File.ReadAllBytes(box.Main);
        byte[] managed = File.ReadAllBytes(box.Managed);

        True(box.Service.ResetGain() is null);

        Bytes(main, File.ReadAllBytes(box.Main));
        Bytes(managed, File.ReadAllBytes(box.Managed));
        Equal(2, Directory.GetFiles(box.Directory).Length);
        True(box.Service.LastBackupPath is null);
    }),
    ("Reset reads the latest integrated device instead of a previously loaded device", () =>
    {
        const string changedDeviceId = "{0.0.0.00000000}.{4F3A1880-282D-4D51-9BA5-340A42A7BD2F}";
        using Sandbox box = new();
        box.Service.Apply(new(200, true, DeviceId));
        Equal(DeviceId, box.Service.ReadSettings()!.DeviceId);
        new ApoConfigurationService(box.Directory).Apply(new(175, true, changedDeviceId));

        Equal(new BoosterSettings(100, false, changedDeviceId), box.Service.ResetGain());

        Equal(new BoosterSettings(100, false, changedDeviceId), box.Service.ReadSettings());
        True(File.ReadAllText(box.Managed).Contains("Device: {4f3a1880-282d-4d51-9ba5-340a42a7bd2f}\r\n", StringComparison.Ordinal));
    }),
    ("Reset preserves external EQ changes made after integration", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(200, true, DeviceId));
        File.AppendAllText(box.Main, "# user's later EQ\r\nFilter: ON PK Fc 500 Hz Gain -4 dB Q 1\r\n");
        byte[] main = File.ReadAllBytes(box.Main);

        box.Service.ResetGain();

        Bytes(main, File.ReadAllBytes(box.Main));
        Equal(new BoosterSettings(100, false, DeviceId), box.Service.ReadSettings());
        Equal(1, Directory.GetFiles(box.Directory, "*.bak").Length);
    }),
    ("Reset refuses externally damaged configuration without overwriting it", () =>
    {
        foreach (string damage in new[] { "managed", "markers", "conditional" })
        {
            using Sandbox box = new();
            box.Service.Apply(new(200, true, DeviceId));
            if (damage == "managed")
                File.AppendAllText(box.Managed, "# manual edit\r\n");
            else if (damage == "markers")
                File.WriteAllText(box.Main, File.ReadAllText(box.Main).Replace("Include: sesplus.txt", "Include: user.txt", StringComparison.Ordinal));
            else
                File.AppendAllText(box.Main, "If: true\r\n");
            byte[] main = File.ReadAllBytes(box.Main);
            byte[] managed = File.ReadAllBytes(box.Managed);
            string[] files = Directory.GetFiles(box.Directory).Order().ToArray();

            Throws<InvalidDataException>(() => box.Service.ResetGain());

            Bytes(main, File.ReadAllBytes(box.Main));
            Bytes(managed, File.ReadAllBytes(box.Managed));
            True(files.SequenceEqual(Directory.GetFiles(box.Directory).Order()));
        }
    }),
    ("Reset reports write failures and preserves the active gain for a retry", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(200, true, DeviceId));
        byte[] main = File.ReadAllBytes(box.Main);
        byte[] managed = File.ReadAllBytes(box.Managed);

        using (FileStream locked = new(box.Managed, FileMode.Open, FileAccess.Read, FileShare.Read))
            Throws<IOException>(() => box.Service.ResetGain());

        Bytes(main, File.ReadAllBytes(box.Main));
        Bytes(managed, File.ReadAllBytes(box.Managed));
        Equal(new BoosterSettings(200, true, DeviceId), box.Service.ReadSettings());
        True(!Directory.GetFiles(box.Directory, ".sesplus-*.tmp").Any());
        box.Service.ResetGain();
        Equal(new BoosterSettings(100, false, DeviceId), box.Service.ReadSettings());
    }),
    ("Config numbers are invariant under Turkish and German cultures", () =>
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "tr-TR", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                using Sandbox box = new();
                box.Service.Apply(new(150, true, DeviceId));
                True(File.ReadAllText(box.Managed).Contains("Preamp: 3.521825 dB\r\n", StringComparison.Ordinal));
                Equal(150, box.Service.ReadSettings()!.Percent);
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }),
    ("Booster does not invent a limiter or rewrite existing preamps", () =>
    {
        using Sandbox box = new("Preamp: 12 dB\nFilter: ON PK Fc 100 Hz Gain 8 dB Q 1\n");
        box.Service.Apply(new(200, true, DeviceId));
        True(File.ReadAllText(box.Managed).Contains("Preamp: 6.0206 dB", StringComparison.Ordinal));
        True(File.ReadAllText(box.Main).EndsWith("Preamp: 12 dB\nFilter: ON PK Fc 100 Hz Gain 8 dB Q 1\n", StringComparison.Ordinal));
    }),
    ("Remove preserves external edits made after integration", () =>
    {
        using Sandbox box = new("# original\nDevice: HDMI\nPreamp: -2 dB\n");
        box.Service.Apply(new(150, true, DeviceId));
        string edited = File.ReadAllText(box.Main).Replace("Preamp: -2 dB", "Preamp: -5 dB", StringComparison.Ordinal) + "# appended\nChannel: R\n";
        File.WriteAllText(box.Main, edited, new UTF8Encoding(false));
        box.Service.RemoveIntegration();
        Equal("# original\nDevice: HDMI\nPreamp: -5 dB\n# appended\nChannel: R\n", File.ReadAllText(box.Main));
        True(!File.Exists(box.Managed));
        True(!box.Service.IsIntegrated);
        byte[] removed = File.ReadAllBytes(box.Main);
        box.Service.RemoveIntegration();
        Bytes(removed, File.ReadAllBytes(box.Main));
    }),
    ("Apply rereads external edits and keeps them", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(150, true, DeviceId));
        File.AppendAllText(box.Main, "\n# a new external edit\n", new UTF8Encoding(false));
        box.Service.Apply(new(160, true, DeviceId));
        True(File.ReadAllText(box.Main).EndsWith("\n# a new external edit\n", StringComparison.Ordinal));
    }),
    ("An unrelated sesplus.txt is protected", () =>
    {
        using Sandbox box = new();
        File.WriteAllText(box.Managed, "# user's file\nPreamp: -12 dB\n");
        byte[] main = File.ReadAllBytes(box.Main);
        byte[] managed = File.ReadAllBytes(box.Managed);
        Throws<InvalidDataException>(() => box.Service.Apply(new(200, true, DeviceId)));
        Bytes(main, File.ReadAllBytes(box.Main));
        Bytes(managed, File.ReadAllBytes(box.Managed));
        True(box.Service.LastBackupPath is null);
    }),
    ("Manual edits to generated settings are protected", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(150, true, DeviceId));
        File.AppendAllText(box.Managed, "# manual edit\r\n");
        byte[] managed = File.ReadAllBytes(box.Managed);
        Throws<InvalidDataException>(() => box.Service.Apply(new(200, true, DeviceId)));
        Throws<InvalidDataException>(() => box.Service.ReadSettings());
        Bytes(managed, File.ReadAllBytes(box.Managed));
        box.Service.RemoveIntegration();
        Bytes(managed, File.ReadAllBytes(box.Managed));
    }),
    ("Edited percentage metadata is rejected when it disagrees with canonical gain", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(150, true, DeviceId));
        File.WriteAllText(box.Managed, File.ReadAllText(box.Managed).Replace("# Percent: 150", "# Percent: 999", StringComparison.Ordinal), new UTF8Encoding(false));
        Throws<InvalidDataException>(() => box.Service.ReadSettings());
    }),
    ("Malformed, duplicated and edited markers fail without mutation", () =>
    {
        string begin = "# SesPlus managed integration begin (v1)\n";
        string end = "# SesPlus managed integration end (v1)\n";
        foreach (string malformed in new[] { begin, end, begin + end, begin + "Include: user.txt\n" + end,
            begin + "Include: sesplus.txt\n" + end + begin + "Include: sesplus.txt\n" + end,
            "# SesPlus managed integration begin (v2)\nInclude: sesplus.txt\n" + end })
        {
            using Sandbox box = new(malformed);
            byte[] before = File.ReadAllBytes(box.Main);
            Throws<InvalidDataException>(() => box.Service.Apply(new(200, true, DeviceId)));
            Throws<InvalidDataException>(() => box.Service.RemoveIntegration());
            Bytes(before, File.ReadAllBytes(box.Main));
            True(!File.Exists(box.Managed));
        }
    }),
    ("Unmarked includes of sesplus.txt cannot cause duplicate gain", () =>
    {
        foreach (string command in new[] { "Include: sesplus.txt", "Include: C:\\equalizer\\sesplus.txt", "Include: \"sesplus.txt\"", "Include: ../config/SESPLUS.TXT" })
        {
            using Sandbox box = new(command);
            Throws<InvalidDataException>(() => box.Service.Apply(new(200, true, DeviceId)));
            True(!File.Exists(box.Managed));
        }
    }),
    ("Commented includes do not block installation", () =>
    {
        using Sandbox box = new("# Include: sesplus.txt\n");
        box.Service.Apply(new(150, true, DeviceId));
        True(box.Service.IsIntegrated);
    }),
    ("Closed conditionals remain intact; invalid conditionals are rejected", () =>
    {
        using (Sandbox box = new("If: outputChannelCount == 2\nChannel: L\nPreamp: -2 dB\nEndIf:\n"))
        {
            box.Service.Apply(new(200, true, DeviceId));
            True(box.Service.IsIntegrated);
        }
        foreach (string invalid in new[] { "If: true\nPreamp: 0 dB\n", "EndIf:\n", "Else:\n" })
        {
            using Sandbox box = new(invalid);
            Throws<InvalidDataException>(() => box.Service.Apply(new(200, true, DeviceId)));
            True(!File.Exists(box.Managed));
        }
    }),
    ("Broken reference can be repaired without deleting main config", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(150, true, DeviceId));
        File.Delete(box.Managed);
        True(!box.Service.IsIntegrated);
        box.Service.Apply(new(160, true, DeviceId));
        Equal(160, box.Service.ReadSettings()!.Percent);
    }),
    ("Invalid settings do not modify configuration", () =>
    {
        using Sandbox box = new();
        byte[] main = File.ReadAllBytes(box.Main);
        foreach (int percent in new[] { int.MinValue, -1, 0 })
            Throws<ArgumentOutOfRangeException>(() => box.Service.Apply(new(percent, true, DeviceId)));
        Throws<ArgumentException>(() => box.Service.Apply(new(200, true, "all")));
        Throws<ArgumentNullException>(() => box.Service.Apply(null!));
        Bytes(main, File.ReadAllBytes(box.Main));
        True(!File.Exists(box.Managed));
        Equal(1, Directory.GetFiles(box.Directory).Length);
        True(box.Service.LastBackupPath is null);
    }),
    ("Nonpositive custom settings preserve existing gain and configuration exactly", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(500, true, DeviceId));
        byte[] main = File.ReadAllBytes(box.Main);
        byte[] managed = File.ReadAllBytes(box.Managed);
        string backupPath = box.Service.LastBackupPath!;
        string[] files = Directory.GetFiles(box.Directory).Order().ToArray();
        foreach (int percent in new[] { int.MinValue, -1, 0 })
        {
            Throws<ArgumentOutOfRangeException>(() => box.Service.Apply(new(percent, true, DeviceId)));
            Throws<ArgumentOutOfRangeException>(() => box.Service.Apply(new(percent, false, DeviceId)));
        }
        Bytes(main, File.ReadAllBytes(box.Main));
        Bytes(managed, File.ReadAllBytes(box.Managed));
        Equal(new BoosterSettings(500, true, DeviceId), box.Service.ReadSettings());
        Equal(backupPath, box.Service.LastBackupPath);
        True(files.SequenceEqual(Directory.GetFiles(box.Directory).Order()));
    }),
    ("Failed main replacement rolls back new sibling and leaves no dangling Include", () =>
    {
        using Sandbox box = new();
        byte[] main = File.ReadAllBytes(box.Main);
        using (FileStream locked = new(box.Main, FileMode.Open, FileAccess.Read, FileShare.Read))
            Throws<IOException>(() => box.Service.Apply(new(200, true, DeviceId)));
        Bytes(main, File.ReadAllBytes(box.Main));
        True(!File.Exists(box.Managed));
        True(!Directory.GetFiles(box.Directory, ".sesplus-*.tmp").Any());
    }),
    ("Short-lived replacement lock is retried and integration succeeds", () =>
    {
        using Sandbox box = new();
        byte[] original = File.ReadAllBytes(box.Main);
        using FileStream locked = new(box.Main, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ManualResetEventSlim stop = new();
        Exception? releaseFailure = null;
        Thread release = new(() =>
        {
            try
            {
                if (!WaitForPendingMainReplacement(box, stop)) return;
                Thread.Sleep(80);
                locked.Dispose();
            }
            catch (Exception exception) { releaseFailure = exception; }
        });
        release.Start();
        try { box.Service.Apply(new(200, true, DeviceId)); }
        finally { stop.Set(); release.Join(); }

        True(releaseFailure is null);
        Equal(new BoosterSettings(200, true, DeviceId), box.Service.ReadSettings());
        True(File.ReadAllBytes(box.Main).AsSpan().EndsWith(original));
        Bytes(original, File.ReadAllBytes(box.Service.LastBackupPath!));
        True(!Directory.GetFiles(box.Directory, ".sesplus-*.tmp").Any());
    }),
    ("Replacement retry refuses an external edit and rolls back the new sibling", () =>
    {
        using Sandbox box = new();
        byte[] original = File.ReadAllBytes(box.Main);
        const string externalEdit = "# another editor changed the main config\r\n";
        using FileStream locked = new(box.Main, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using ManualResetEventSlim stop = new();
        Exception? editFailure = null;
        Thread edit = new(() =>
        {
            try
            {
                if (!WaitForPendingMainReplacement(box, stop)) return;
                Thread.Sleep(50);
                File.AppendAllText(box.Main, externalEdit, new UTF8Encoding(false));
            }
            catch (Exception exception) { editFailure = exception; }
        });
        edit.Start();
        try { Throws<IOException>(() => box.Service.Apply(new(200, true, DeviceId))); }
        finally { stop.Set(); edit.Join(); }

        True(editFailure is null);
        Bytes([.. original, .. Encoding.UTF8.GetBytes(externalEdit)], File.ReadAllBytes(box.Main));
        True(!File.Exists(box.Managed));
        True(!Directory.GetFiles(box.Directory, ".sesplus-*.tmp").Any());
    }),
    ("Persistent replacement lock exhausts retries and restores the previous gain", () =>
    {
        using Sandbox box = new();
        box.Service.Apply(new(150, true, DeviceId));
        string integrated = File.ReadAllText(box.Main);
        int blockLength = integrated.IndexOf("# original config", StringComparison.Ordinal);
        // Moving the valid integration makes Apply update the main file as well as gain.
        File.WriteAllText(box.Main, integrated[blockLength..] + integrated[..blockLength], new UTF8Encoding(false));
        byte[] main = File.ReadAllBytes(box.Main);
        byte[] managed = File.ReadAllBytes(box.Managed);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        using (FileStream locked = new(box.Main, FileMode.Open, FileAccess.Read, FileShare.Read))
            Throws<IOException>(() => box.Service.Apply(new(200, true, DeviceId)));

        elapsed.Stop();
        True(elapsed.Elapsed < TimeSpan.FromSeconds(2));
        Bytes(main, File.ReadAllBytes(box.Main));
        Bytes(managed, File.ReadAllBytes(box.Managed));
        Equal(new BoosterSettings(150, true, DeviceId), box.Service.ReadSettings());
        True(!Directory.GetFiles(box.Directory, ".sesplus-*.tmp").Any());
    })
];

foreach ((string name, Action test) in tests)
{
    try
    {
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine("FAIL " + name + ": " + exception);
    }
}
Console.WriteLine($"{passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

static bool WaitForPendingMainReplacement(Sandbox box, ManualResetEventSlim stop)
{
    var elapsed = System.Diagnostics.Stopwatch.StartNew();
    while (!stop.IsSet && elapsed.Elapsed < TimeSpan.FromSeconds(2))
    {
        if (File.Exists(box.Managed) && Directory.GetFiles(box.Directory, ".sesplus-*.tmp").Length > 0)
            return true;
        Thread.Sleep(1);
    }
    return false;
}

static void Preservation(Encoding encoding, string text)
{
    using Sandbox box = new();
    byte[] original = [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    File.WriteAllBytes(box.Main, original);
    box.Service.Apply(new(150, true, DeviceId));
    byte[] modified = File.ReadAllBytes(box.Main);
    True(modified.AsSpan().StartsWith(encoding.GetPreamble()));
    True(modified.AsSpan().EndsWith(encoding.GetBytes(text)));
    box.Service.RemoveIntegration();
    Bytes(original, File.ReadAllBytes(box.Main));
}

static void True(bool value)
{
    if (!value) throw new Exception("Expected true.");
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}; got {actual}.");
}

static void Bytes(byte[] expected, byte[] actual) => True(expected.AsSpan().SequenceEqual(actual));

static void Near(double expected, double actual, double tolerance)
{
    if (Math.Abs(expected - actual) > tolerance)
        throw new Exception($"Expected {expected}; got {actual}.");
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name + ".");
}

sealed class Sandbox : IDisposable
{
    public Sandbox(string initial = "# original config\r\nPreamp: -3 dB\r\n")
    {
        Directory = Path.Combine(AppContext.BaseDirectory, "test-data", "sesplus-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Main = Path.Combine(Directory, "config.txt");
        Managed = Path.Combine(Directory, "sesplus.txt");
        File.WriteAllText(Main, initial, new UTF8Encoding(false));
        Service = new ApoConfigurationService(Directory);
    }

    public string Directory { get; }
    public string Main { get; }
    public string Managed { get; }
    public ApoConfigurationService Service { get; }

    public void Dispose() => System.IO.Directory.Delete(Directory, true);
}
