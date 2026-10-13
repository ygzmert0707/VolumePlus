using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using SesPlus.Core;

namespace SesPlus;

public partial class CustomPercentageDialog : Window
{
    private readonly string _language;
    private bool _ready;
    public int SelectedPercent { get; private set; }
    internal bool IsAcceptanceEnabled => AcceptButton.IsEnabled;

    public CustomPercentageDialog(string language, int currentPercent)
    {
        _language = Localization.NormalizeLanguage(language);
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            try { var dark = 1; DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int)); }
            catch (DllNotFoundException) { }
        };
        string T(string key) => Localization.Get(key, _language);
        Title = "Volume + · " + T("customTitle");
        DialogHeading.Text = T("customTitle"); DialogDescription.Text = T("customDescription");
        ValueLabel.Text = T("customValueLabel"); InvalidMessage.Text = T("customInvalid");
        WarningHeading.Text = T("customWarningTitle"); WarningDescription.Text = T("customWarning");
        AcknowledgementText.Text = T("customAcknowledge"); AcceptButton.Content = T("customApply"); CancelButton.Content = T("cancel");
        AutomationProperties.SetName(ValueInput, T("customValueLabel"));
        ValueInput.Text = currentPercent.ToString(CultureInfo.InvariantCulture);
        _ready = true;
        Evaluate();
        Loaded += (_, _) => { ValueInput.Focus(); ValueInput.SelectAll(); };
    }

    private bool TryRead(out int percent) => int.TryParse(ValueInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out percent) && percent >= GainCalculator.MinimumPercent;

    private void Evaluate()
    {
        var valid = TryRead(out var percent);
        var requiresAcknowledgement = valid && percent >= GainCalculator.StandardMaximumPercent;
        Acknowledgement.Visibility = requiresAcknowledgement ? Visibility.Visible : Visibility.Collapsed;
        InvalidMessage.Visibility = !valid && ValueInput.Text.Length != 0 ? Visibility.Visible : Visibility.Collapsed;
        AcceptButton.IsEnabled = valid && (!requiresAcknowledgement || Acknowledgement.IsChecked == true);
    }

    private void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        Acknowledgement.IsChecked = false;
        Evaluate();
    }

    private void AcknowledgementChanged(object sender, RoutedEventArgs e) { if (_ready) Evaluate(); }

    private void ApplyClicked(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var percent) || !AcceptButton.IsEnabled) return;
        SelectedPercent = percent;
        DialogResult = true;
    }

    internal void SetInputForPreview(string input, bool acknowledge)
    {
        ValueInput.Text = input;
        Acknowledgement.IsChecked = acknowledge;
        Evaluate();
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
