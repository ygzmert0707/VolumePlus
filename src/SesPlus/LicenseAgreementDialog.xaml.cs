using SesPlus.Services;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace SesPlus;

public partial class LicenseAgreementDialog : Window
{
    private readonly bool _requireAcceptance;
    private readonly string[] _documents;
    internal bool CanAccept => AcceptButton.IsEnabled;

    public LicenseAgreementDialog(string language, bool requireAcceptance)
    {
        _requireAcceptance = requireAcceptance;
        InitializeComponent();
        string T(string key) => LegalStrings.Get(key, language);
        Title = "Volume + · " + T("title"); Heading.Text = T("title"); Introduction.Text = T("intro");
        AgreementText.Text = T("agree"); AcceptButton.Content = T("accept"); DeclineButton.Content = T(requireAcceptance ? "decline" : "close");
        _documents = [LegalResources.Read("microsoft/DOTNET-LIBRARY-LICENSE.txt"), LegalResources.Read("microsoft/WINDOWS-SDK-LICENSE.txt"), LegalResources.Read("LICENSE"), LegalResources.Notices];
        DocumentSelector.ItemsSource = new[] { ".NET Library License", "Windows SDK License", T("source"), T("notices") };
        DocumentSelector.SelectedIndex = 0;
        AgreementCheck.Visibility = requireAcceptance ? Visibility.Visible : Visibility.Collapsed;
        AcceptButton.Visibility = requireAcceptance ? Visibility.Visible : Visibility.Collapsed;
        AcceptButton.IsEnabled = false;
        ShowInTaskbar = requireAcceptance;
        SourceInitialized += (_, _) =>
        {
            try { var dark = 1; DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int)); }
            catch (DllNotFoundException) { }
        };
    }

    private void DocumentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_documents is not null && DocumentSelector.SelectedIndex >= 0) DocumentText.Text = _documents[DocumentSelector.SelectedIndex];
    }

    private void AgreementChanged(object sender, RoutedEventArgs e) { if (AcceptButton is not null) AcceptButton.IsEnabled = _requireAcceptance && AgreementCheck.IsChecked == true; }
    private void AcceptClicked(object sender, RoutedEventArgs e) { if (CanAccept) DialogResult = true; }
    internal void AcknowledgeForPreview(bool agree) => AgreementCheck.IsChecked = agree;
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
