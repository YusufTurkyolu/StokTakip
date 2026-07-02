using System.Windows;
using System.Windows.Input;

namespace StokTakip.UI.Views;

public partial class PinDialog : Window
{
    // Gerçek uygulamada: appsettings.json'dan oku + BCrypt hash karşılaştır
    private const string AdminPin = "1234";

    public PinDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => PbPin.Focus();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e) => Validate();
    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void PbPin_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Validate();
    }

    private void Validate()
    {
        if (PbPin.Password == AdminPin)
        {
            DialogResult = true;
        }
        else
        {
            TxtError.Text = "Hatalı PIN. Tekrar deneyin.";
            TxtError.Visibility = Visibility.Visible;
            PbPin.Clear();
            PbPin.Focus();
        }
    }
}
