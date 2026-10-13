using SesPlus.Core;
using SesPlus.Services;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SesPlus;

public partial class MainWindow : Window
{
    private readonly bool _preview;
    private readonly AudioDeviceService _devices = new();
    private readonly DispatcherTimer _gainDebounce;
    private Preferences _preferences;
    private ApoInstallation? _installation;
    private ApoConfigurationService? _configuration;
    private BoosterSettings? _saved;
    private bool _loading = true;
    private bool _ready;
    private string _language = "en";
    private string _status = "setup";
    private string _error = "";
    private bool _adminRelaunch;
    private bool _resetOnCloseInPreview;
    private int _percent = 100;

    public MainWindow(bool preview = false)
    {
        _preview = preview;
        _preferences = preview ? new Preferences() : PreferenceStore.Load();
        _language = _preferences.Language;
        InitializeComponent();
        _gainDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _gainDebounce.Tick += (_, _) => { _gainDebounce.Stop(); if (!_loading && _saved?.Enabled == true && SelectedDevice?.Id == _saved.DeviceId) SaveGain(true); };
        LanguageBox.ItemsSource = Localization.SupportedLanguages;
        LanguageBox.SelectedValue = _language;
        SetPercent(_preferences.Percent, false);
        SourceInitialized += (_, _) =>
        {
            try { var dark = 1; DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int)); }
            catch (DllNotFoundException) { }
        };
        Loaded += (_, _) => { RefreshConnection(); _ready = true; _loading = false; Localize(); };
        Closing += (_, args) =>
        {
            _gainDebounce.Stop();
            if ((!_preview || _resetOnCloseInPreview) && !_adminRelaunch)
            {
                try
                {
                    _saved = _configuration?.ResetGain();
                    SetPercent(100, false);
                    SavePreferences();
                }
                catch (Exception ex)
                {
                    args.Cancel = true;
                    _status = "error";
                    _error = ex.Message;
                    if (ex is UnauthorizedAccessException) AdminButton.Visibility = Visibility.Visible;
                    UpdateActions(); UpdateStatus();
                    MessageBox.Show(this, string.Format(T("closeResetFailed"), ex.Message), "Volume +", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else if (!_preview && _ready) SavePreferences();
        };
    }

    private AudioOutputDevice? SelectedDevice => DeviceBox.SelectedItem as AudioOutputDevice;
    private int Percent => _percent;
    internal string PreviewDiagnostics => _status == "error" ? throw new InvalidOperationException(_error) : $"Status: {_status}; output devices: {DeviceBox.Items.Count}";
    private string T(string key) => Localization.Get(key, _language);
    private string P(int percent) => (percent / 100.0).ToString("P0", Localization.GetCulture(_language));

    public void SetLanguageForPreview(string language)
    {
        _language = Localization.NormalizeLanguage(language);
        LanguageBox.SelectedValue = _language;
        Localize();
    }

    internal void SetSmallLayoutForPreview()
    {
        Width = MinWidth;
        Height = MinHeight;
        AdminButton.Visibility = Visibility.Visible;
        UpdateLayout();
    }

    internal void ConfigureCloseResetPreview(ApoConfigurationService configuration)
    {
        _configuration = configuration;
        _saved = configuration.ReadSettings();
        _resetOnCloseInPreview = true;
    }

    internal void SetCustomLevelForPreview(int percent)
    {
        SetPercent(percent, false);
        UpdateLayout();
    }

    internal void ValidatePanelAlignmentForPreview()
    {
        UpdateLayout();
        var statusBottom = StatusCard.TranslatePoint(new Point(0, StatusCard.ActualHeight), AudioLayout).Y;
        var outputBottom = OutputCard.TranslatePoint(new Point(0, OutputCard.ActualHeight), AudioLayout).Y;
        if (Math.Abs(statusBottom - outputBottom) > 0.5)
            throw new InvalidOperationException($"Panel bottoms are misaligned: {statusBottom} / {outputBottom}.");
        if (Height >= 860 && AudioLayout.ActualHeight > BodyScroll.ViewportHeight + 1)
            throw new InvalidOperationException("The normal-size layout unexpectedly hides the bottom panels.");
    }

    private void Localize()
    {
        if (BrandCaption is null) return;
        Title = T("title");
        BrandCaption.Text = T("brand"); LanguageLabel.Text = T("language"); GainHeading.Text = T("gain");
        HeroTitle.Text = T("hero"); HeroSubtitle.Text = T("subtitle"); DbCaption.Text = T("amplification");
        NormalPreset.Content = T("normal"); ResetButton.Content = T("reset"); OutputHeading.Text = T("output");
        DeviceCaption.Text = T("device"); DeviceHelp.Text = T("deviceHelp");
        SetupIntro.Text = T("setupIntro"); SetupStepOne.Text = T("stepOne"); DownloadButton.Content = T("download");
        SetupStepTwo.Text = T("stepTwo"); DeviceSetupButton.Content = T("selector"); SetupStepThree.Text = T("stepThree");
        RefreshButton.Content = T("refresh"); AdminButton.Content = T("admin"); DisconnectButton.Content = T("disconnect");
        FooterHint.Text = T("footer");
        AutoResetHint.Text = T("autoResetNote");
        LicenseButton.Content = LegalStrings.Get("licenses", _language);
        GainRangeBadge.Text = P(100) + " - " + P(200);
        CustomPercentageButton.Content = T("customButton");
        Preset125.Content = P(125); Preset150.Content = P(150); Preset200.Content = P(200);
        AutomationProperties.SetName(GainSlider, T("sliderName"));
        AutomationProperties.SetName(DeviceBox, T("device"));
        AutomationProperties.SetName(LanguageBox, T("language"));
        UpdateGainDisplay(); UpdateActions(); UpdateStatus();
    }

    private void UpdateGainDisplay()
    {
        if (PercentDisplay is null) return;
        PercentDisplay.Text = P(Percent);
        PercentDisplay.FontSize = Math.Max(24, 76 - Math.Max(0, PercentDisplay.Text.Length - 5) * 5);
        DbDisplay.Text = GainCalculator.ToDecibels(Percent).ToString("+0.00;-0.00;+0.00", Localization.GetCulture(_language)) + " dB";
        CustomLevelHint.Visibility = Percent is < 100 or > 200 ? Visibility.Visible : Visibility.Collapsed;
        CustomLevelHint.Text = string.Format(T("customSliderNote"), P(Percent));
    }

    private void UpdateActions()
    {
        var same = _saved is not null && SelectedDevice?.Id == _saved.DeviceId && Percent == _saved.Percent;
        var neutral = Percent == 100 && _saved?.Enabled != true;
        ApplyButton.Content = T(neutral ? "apply" : same ? (_saved!.Enabled ? "pause" : "resume") : "apply");
        ApplyButton.IsEnabled = _configuration is not null && SelectedDevice is not null && !neutral;
        ResetButton.IsEnabled = Percent != 100 || _saved?.Enabled == true;
        DeviceSetupButton.IsEnabled = _installation?.SelectorPath is not null;
        DisconnectButton.IsEnabled = _saved is not null;
        EngineStatus.Text = T(_installation is null ? "missing" : "installed");
        EngineStatus.Foreground = Brush(_installation is null ? "#F4C978" : "#D3F585");
    }

    private void UpdateStatus()
    {
        if (StatusHeading is null) return;
        DetailsButton.Visibility = _status == "error" ? Visibility.Visible : Visibility.Collapsed;
        DetailsButton.Content = T("details");
        StatusDot.Fill = Brush(_status is "saved" or "paused" or "removed" ? "#D3F585" : _status == "error" ? "#FF9999" : "#F4C978");
        switch (_status)
        {
            case "saved":
                StatusHeading.Text = string.Format(T("savedTitle"), P(_saved?.Percent ?? Percent));
                StatusDescription.Text = string.Format(T("savedDetail"), SelectedDevice?.Name ?? ""); break;
            case "paused": StatusHeading.Text = T("pausedTitle"); StatusDescription.Text = T("pausedDetail"); break;
            case "removed": StatusHeading.Text = T("removedTitle"); StatusDescription.Text = T("removedDetail"); break;
            case "noDevice": StatusHeading.Text = T("noDeviceTitle"); StatusDescription.Text = T("noDeviceDetail"); break;
            case "error": StatusHeading.Text = T("errorTitle"); StatusDescription.Text = AdminButton.Visibility == Visibility.Visible ? T("permission") : T("errorDetail"); break;
            case "pending": StatusHeading.Text = string.Format(T("pendingTitle"), P(Percent)); StatusDescription.Text = T("pendingDetail"); break;
            case "ready": StatusHeading.Text = T("readyTitle"); StatusDescription.Text = T("readyDetail"); break;
            default: StatusHeading.Text = T("setupTitle"); StatusDescription.Text = T("setupDetail"); break;
        }
    }

    private void RefreshConnection()
    {
        _gainDebounce.Stop();
        var wasLoading = _loading; _loading = true;
        try
        {
            var previous = SelectedDevice?.Id ?? _preferences.DeviceId;
            _installation = ApoLocator.Find();
            _configuration = _installation is null ? null : new ApoConfigurationService(_installation.ConfigDirectory);
            _saved = _configuration?.ReadSettings();
            var devices = _devices.GetOutputDevices();
            DeviceBox.ItemsSource = devices;
            DeviceBox.SelectedItem = devices.FirstOrDefault(d => d.Id == (_saved?.DeviceId ?? previous)) ?? devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
            if (_saved is not null && SelectedDevice?.Id == _saved.DeviceId) SetPercent(_saved.Percent, false);
            _status = SelectedDevice is null ? "noDevice" : _installation is null ? "setup" : _saved is not null && _saved.DeviceId == SelectedDevice.Id ? (_saved.Enabled ? "saved" : "paused") : "ready";
            AdminButton.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { _status = "error"; _error = string.Format(T("refreshFailure"), ex.Message); }
        finally { _loading = wasLoading; Localize(); }
    }

    private void SaveGain(bool enabled)
    {
        if (_configuration is null || SelectedDevice is null || _preview) return;
        try
        {
            var target = new BoosterSettings(Percent, enabled && Percent != 100, SelectedDevice.Id);
            _configuration.Apply(target);
            _saved = target;
            _status = enabled && Percent != 100 ? "saved" : "paused";
            AdminButton.Visibility = Visibility.Collapsed;
            SavePreferences();
        }
        catch (UnauthorizedAccessException) { _error = T("permission"); _status = "error"; AdminButton.Visibility = Visibility.Visible; }
        catch (Exception ex) { _error = ex.Message; _status = "error"; }
        UpdateActions(); UpdateStatus();
    }

    private void SavePreferences()
    {
        if (_preview) return;
        _preferences = _preferences with { Language = _language, Percent = Percent, DeviceId = SelectedDevice?.Id };
        try { PreferenceStore.Save(_preferences); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { Debug.WriteLine(ex); }
    }

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is not LanguageOption item) return;
        _language = item.Code;
        if (!_loading) { Localize(); SavePreferences(); }
    }

    private void GainChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || !_ready) return;
        _percent = (int)Math.Round(e.NewValue);
        ChosenPercentChanged();
    }

    private void SetPercent(int percent, bool updateState = true)
    {
        _ = GainCalculator.ToDecibels(percent);
        _gainDebounce.Stop();
        var wasLoading = _loading;
        _loading = true;
        _percent = percent;
        GainSlider.Value = Math.Clamp(percent, 100, 200);
        _loading = wasLoading;
        UpdateGainDisplay();
        UpdateActions();
        if (updateState && _ready && !_loading) ChosenPercentChanged();
    }

    private void ChosenPercentChanged()
    {
        UpdateGainDisplay();
        _gainDebounce.Stop();
        if (_saved?.Enabled == true && SelectedDevice?.Id == _saved.DeviceId && !_preview) _gainDebounce.Start();
        else if (_configuration is not null && SelectedDevice is not null) _status = Percent == 100 && _saved?.Enabled != true ? "paused" : "pending";
        UpdateActions(); UpdateStatus();
    }

    private void DeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !_ready) return;
        _gainDebounce.Stop();
        _status = SelectedDevice is null ? "noDevice" : _installation is null ? "setup" : "pending";
        UpdateActions(); UpdateStatus(); SavePreferences();
    }

    private void PresetClicked(object sender, RoutedEventArgs e) => SetPercent(int.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture));
    private void CustomPercentageClicked(object sender, RoutedEventArgs e)
    {
        var hadPendingChange = _gainDebounce.IsEnabled;
        _gainDebounce.Stop();
        var dialog = new CustomPercentageDialog(_language, Percent) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            if (hadPendingChange) _gainDebounce.Start();
            return;
        }
        SetPercent(dialog.SelectedPercent, false);
        if (_configuration is not null && SelectedDevice is not null) SaveGain(true);
        else ChosenPercentChanged();
    }

    private void NormalSliderEngaged(object sender, MouseButtonEventArgs e)
    {
        if (Percent is < 100 or > 200) SetPercent((int)Math.Round(GainSlider.Value));
    }

    private void NormalSliderKeyPressed(object sender, KeyEventArgs e)
    {
        if ((e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown) && (Percent is < 100 or > 200))
            SetPercent((int)Math.Round(GainSlider.Value));
    }
    private void ApplyClicked(object sender, RoutedEventArgs e)
    {
        _gainDebounce.Stop();
        var current = _saved?.Enabled == true && _saved.DeviceId == SelectedDevice?.Id && _saved.Percent == Percent;
        SaveGain(!current);
    }

    private void ResetClicked(object sender, RoutedEventArgs e)
    {
        _gainDebounce.Stop();
        SetPercent(100, false);
        if (_saved is not null && _configuration is not null)
        {
            // Reset the actual integrated device even when another output is selected.
            try
            {
                _configuration.Apply(_saved with { Percent = 100, Enabled = false });
                _saved = _saved with { Percent = 100, Enabled = false };
                _status = "paused";
            }
            catch (UnauthorizedAccessException) { _status = "error"; _error = T("permission"); AdminButton.Visibility = Visibility.Visible; }
            catch (Exception ex) { _status = "error"; _error = ex.Message; }
        }
        SavePreferences(); UpdateActions(); UpdateStatus();
    }

    private void DisconnectClicked(object sender, RoutedEventArgs e)
    {
        _gainDebounce.Stop();
        if (_configuration is null || _preview) return;
        try { _configuration.RemoveIntegration(); _saved = null; _status = "removed"; }
        catch (UnauthorizedAccessException) { _status = "error"; _error = T("permission"); AdminButton.Visibility = Visibility.Visible; }
        catch (Exception ex) { _status = "error"; _error = ex.Message; }
        UpdateActions(); UpdateStatus();
    }

    private void RefreshClicked(object sender, RoutedEventArgs e) => RefreshConnection();
    private void LicenseClicked(object sender, RoutedEventArgs e) => new LicenseAgreementDialog(_language, false) { Owner = this }.ShowDialog();
    private void DetailsClicked(object sender, RoutedEventArgs e) => MessageBox.Show(this, _error, "Volume + · " + T("details"), MessageBoxButton.OK, MessageBoxImage.Information);
    private void DownloadClicked(object sender, RoutedEventArgs e) => Launch(ApoLocator.OfficialDownloadUrl);
    private void DeviceSetupClicked(object sender, RoutedEventArgs e) { if (_installation?.SelectorPath is { } path) Launch(path); }
    private void AdminClicked(object sender, RoutedEventArgs e)
    {
        if (Environment.ProcessPath is not { } path) return;
        try
        {
            SavePreferences();
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas", Arguments = $"--wait-for-parent {Environment.ProcessId}" });
            _adminRelaunch = true;
            Close();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex) { MessageBox.Show(this, string.Format(T("launchFailure"), ex.Message), "Volume +", MessageBoxButton.OK, MessageBoxImage.Information); }
    }

    private void Launch(string target)
    {
        try
        {
            var start = new ProcessStartInfo(target) { UseShellExecute = true };
            if (System.IO.File.Exists(target)) start.WorkingDirectory = System.IO.Path.GetDirectoryName(target)!;
            Process.Start(start);
        }
        catch (Exception ex) { MessageBox.Show(this, string.Format(T("launchFailure"), ex.Message), "Volume +", MessageBoxButton.OK, MessageBoxImage.Information); }
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
