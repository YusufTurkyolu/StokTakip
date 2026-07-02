using System.Windows;

namespace StokTakip.UI.Views;

public partial class AddItemDialog : Window
{
    public string ItemName { get; private set; } = string.Empty;
    public int InitialStock { get; private set; }
    public int MinThreshold { get; private set; }

    public AddItemDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtItemName.Focus();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        var name = TxtItemName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Ürün adı boş bırakılamaz.");
            return;
        }

        if (!int.TryParse(TxtInitialStock.Text, out int stock) || stock < 0)
        {
            ShowError("Başlangıç stok sayısı geçersiz.");
            return;
        }

        if (!int.TryParse(TxtMinThreshold.Text, out int min) || min < 0)
        {
            ShowError("Minimum eşik değeri geçersiz.");
            return;
        }

        ItemName = name;
        InitialStock = stock;
        MinThreshold = min;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
