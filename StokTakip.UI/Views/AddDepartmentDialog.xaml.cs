using System.Windows;
using System.Windows.Input;

namespace StokTakip.UI.Views;

public partial class AddDepartmentDialog : Window
{
    public string DepartmentName { get; private set; } = string.Empty;

    public AddDepartmentDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtName.Focus();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        var name = TxtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TxtError.Text = "Departman adı boş bırakılamaz.";
            TxtError.Visibility = Visibility.Visible;
            return;
        }

        DepartmentName = name;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TxtName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) BtnAdd_Click(sender, e);
    }
}
