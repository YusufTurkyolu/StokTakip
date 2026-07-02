using System.Windows;
using System.Windows.Input;

namespace StokTakip.UI.Views;

public partial class EditTransactionDialog : Window
{
    public int NewQuantity { get; private set; }

    public EditTransactionDialog(string itemName, int currentQuantity, string typeLabel)
    {
        InitializeComponent();

        TxtItemName.Text = $"Ürün: {itemName}  ({typeLabel})";
        TxtCurrent.Text  = $"Mevcut miktar: {currentQuantity} adet";
        TxtQuantity.Text = currentQuantity.ToString();

        Loaded += (_, _) => { TxtQuantity.Focus(); TxtQuantity.SelectAll(); };
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!int.TryParse(TxtQuantity.Text.Trim(), out int qty) || qty <= 0)
        {
            TxtError.Text = "Lütfen geçerli bir miktar girin (en az 1).";
            TxtError.Visibility = Visibility.Visible;
            return;
        }

        NewQuantity = qty;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TxtQuantity_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) BtnSave_Click(sender, e);
    }
}
