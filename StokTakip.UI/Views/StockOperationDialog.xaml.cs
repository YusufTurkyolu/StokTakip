using System.Windows;
using System.Windows.Controls;
using StokTakip.Data.Models;

namespace StokTakip.UI.Views;

public partial class StockOperationDialog : Window
{
    public int SelectedDepartmentId { get; private set; }
    public int Quantity { get; private set; }

    private readonly int _currentStock;
    private readonly bool _isStockIn;

    public StockOperationDialog(
        string operationType,
        string itemName,
        int currentStock,
        IEnumerable<Department> departments)
    {
        InitializeComponent();

        _currentStock = currentStock;
        _isStockIn = operationType == "StockIn";

        TxtTitle.Text = _isStockIn ? "⬆ Stok Girişi" : "⬇ Stok Çıkışı";
        TxtItemName.Text = $"Ürün: {itemName}";
        TxtStockInfo.Text = $"Mevcut stok: {currentStock} adet";
        BtnConfirm.Content = _isStockIn ? "Giriş Yap" : "Çıkış Yap";

        if (_isStockIn)
        {
            // Girişler genel bütçeden — birim sorulmaz
            LblDepartment.Visibility = Visibility.Collapsed;
            CmbDepartment.Visibility = Visibility.Collapsed;
        }
        else
        {
            var deptList = departments.ToList();
            CmbDepartment.ItemsSource = deptList;
            if (deptList.Any()) CmbDepartment.SelectedIndex = 0;
        }

        Loaded += (_, _) => TxtQuantity.Focus();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!_isStockIn)
        {
            if (CmbDepartment.SelectedValue is not int deptId)
            {
                ShowError("Lütfen bir departman seçin.");
                return;
            }
            SelectedDepartmentId = deptId;
        }

        if (!int.TryParse(TxtQuantity.Text.Trim(), out int qty) || qty <= 0)
        {
            ShowError("Lütfen geçerli bir miktar girin (en az 1).");
            return;
        }

        Quantity = qty;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
