using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SesPlus.Core;
using SesPlus.Services;

namespace SesPlus;

public partial class App : Application
{
    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke = e.Args.Length >= 2 && e.Args[0] == "--smoke-test";
        if (!smoke)
        {
            if (e.Args.Length == 2 && e.Args[0] == "--wait-for-parent" && int.TryParse(e.Args[1], out var parentId))
            {
                try { using var parent = System.Diagnostics.Process.GetProcessById(parentId); parent.WaitForExit(10000); }
                catch (ArgumentException) { }
            }
            _instanceMutex = new Mutex(true, "Local\\SesPlus.VolumeBooster", out var firstInstance);
            if (!firstInstance)
            {
                MessageBox.Show(Localization.Get("alreadyOpen", PreferenceStore.Load().Language), "Volume +");
                Shutdown();
                return;
            }
        }
        if (!smoke && LegalResources.RequiresRuntimeAgreement)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var preferences = PreferenceStore.Load();
            try
            {
                var agreementHash = LegalResources.RuntimeAgreementHash;
                if (preferences.RuntimeLicenseAcceptance != agreementHash)
                {
                    if (new LicenseAgreementDialog(preferences.Language, true).ShowDialog() != true)
                    {
                        Shutdown();
                        return;
                    }
                    PreferenceStore.Save(preferences with { RuntimeLicenseAcceptance = agreementHash });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LegalStrings.Get("saveFailure", preferences.Language), ex.Message), "Volume +", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(1);
                return;
            }
        }
        var window = new MainWindow(smoke);
        MainWindow = window;
        window.Show();
        if (!smoke) ShutdownMode = ShutdownMode.OnLastWindowClose;
        if (smoke)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var output = Path.GetFullPath(e.Args[1]);
                    Directory.CreateDirectory(output);
                    Localization.Validate();
                    LegalStrings.Validate();
                    if (e.Args.Contains("--expect-runtime-agreement", StringComparer.Ordinal) && !LegalResources.RequiresRuntimeAgreement)
                        throw new InvalidOperationException("The self-contained build did not enable its runtime-license startup gate.");
                    if (LegalResources.RuntimeAgreementHash.Length != 64 || !LegalResources.Read("LICENSE").Contains("MIT License", StringComparison.Ordinal))
                        throw new InvalidOperationException("Bundled licensing resources are incomplete.");
                    foreach (var language in Localization.SupportedLanguages)
                    {
                        window.SetLanguageForPreview(language.Code);
                        window.UpdateLayout();
                        window.ValidatePanelAlignmentForPreview();
                        Render(window, Path.Combine(output, $"volumeplus-{language.Code}.png"));
                        var legalDialog = new LicenseAgreementDialog(language.Code, true) { Owner = window };
                        legalDialog.Show();
                        legalDialog.UpdateLayout();
                        if (legalDialog.CanAccept) throw new InvalidOperationException("Runtime terms were accepted without explicit acknowledgement.");
                        legalDialog.AcknowledgeForPreview(true);
                        if (!legalDialog.CanAccept) throw new InvalidOperationException("Explicit runtime acknowledgement was rejected.");
                        Render(legalDialog, Path.Combine(output, $"licenses-{language.Code}.png"));
                        legalDialog.AcknowledgeForPreview(false);
                        if (legalDialog.CanAccept) throw new InvalidOperationException("Unchecked runtime terms stayed accepted.");
                        legalDialog.Close();
                        var customDialog = new CustomPercentageDialog(language.Code, 350) { Owner = window };
                        customDialog.Show();
                        customDialog.UpdateLayout();
                        if (customDialog.IsAcceptanceEnabled) throw new InvalidOperationException("A high custom percentage bypassed acknowledgement.");
                        customDialog.SetInputForPreview("350", true);
                        if (!customDialog.IsAcceptanceEnabled) throw new InvalidOperationException("A confirmed custom percentage was rejected.");
                        Render(customDialog, Path.Combine(output, $"custom-percentage-{language.Code}.png"));
                        foreach (var invalidInput in new[] { "0", "-5", "350.5", "2147483648", "abc", "" })
                        {
                            customDialog.SetInputForPreview(invalidInput, true);
                            if (customDialog.IsAcceptanceEnabled) throw new InvalidOperationException("Invalid custom input was accepted: " + invalidInput);
                        }
                        customDialog.SetInputForPreview("200", false);
                        if (customDialog.IsAcceptanceEnabled) throw new InvalidOperationException("200% bypassed acknowledgement.");
                        customDialog.SetInputForPreview("75", false);
                        if (!customDialog.IsAcceptanceEnabled) throw new InvalidOperationException("A valid lower percentage was rejected.");
                        customDialog.Close();
                    }
                    var diagnostics = window.PreviewDiagnostics;
                    window.SetLanguageForPreview("en");
                    window.SetCustomLevelForPreview(350);
                    window.ValidatePanelAlignmentForPreview();
                    Render(window, Path.Combine(output, "volumeplus-custom-350.png"));
                    window.SetCustomLevelForPreview(int.MaxValue);
                    window.ValidatePanelAlignmentForPreview();
                    Render(window, Path.Combine(output, "volumeplus-custom-large.png"));
                    window.SetCustomLevelForPreview(100);
                    window.SetLanguageForPreview("de");
                    window.SetSmallLayoutForPreview();
                    window.ValidatePanelAlignmentForPreview();
                    Render(window, Path.Combine(output, "volumeplus-de-small.png"));
                    var fixtureDirectory = Path.Combine(output, "close-reset-fixture-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(fixtureDirectory);
                    var originalConfig = "# smoke fixture: preserve existing equalization\r\nPreamp: -3 dB\r\n";
                    File.WriteAllText(Path.Combine(fixtureDirectory, "config.txt"), originalConfig);
                    var fixture = new ApoConfigurationService(fixtureDirectory);
                    fixture.Apply(new BoosterSettings(350, true, "{0.0.0.00000000}.{11111111-2222-3333-4444-555555555555}"));
                    window.ConfigureCloseResetPreview(fixture);
                    window.Close();
                    var afterClose = fixture.ReadSettings();
                    if (window.IsVisible || afterClose is not { Percent: 100, Enabled: false } || !File.ReadAllText(Path.Combine(fixtureDirectory, "config.txt")).EndsWith(originalConfig, StringComparison.Ordinal))
                        throw new InvalidOperationException("The normal-close reset or existing-EQ preservation check failed.");
                    File.WriteAllText(Path.Combine(output, "smoke-test.txt"), "PASS: seven languages, custom dialogs and license dialogs rendered; explicit runtime acceptance and embedded legal texts checked; panel bottom alignment verified; custom input validation and 200%+ acknowledgement checked; normal WPF close reset a 350% fixture to 100%/disabled and preserved existing EQ. " + diagnostics + ". No system audio configuration or user preferences modified.");
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(e.Args[1], "smoke-test.txt"), ex.ToString());
                    Shutdown(1);
                }
            }));
        }
    }

    private static void Render(Window window, string outputPath)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, window.ActualWidth, window.ActualHeight));
        bitmap.Render(background);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(outputPath);
        encoder.Save(file);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
