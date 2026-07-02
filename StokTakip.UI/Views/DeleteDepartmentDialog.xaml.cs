using System.Windows;
using StokTakip.Data.Models;

namespace StokTakip.UI.Views;

public partial class DeleteDepartmentDialog : Window
{
    public int SelectedDepartmentId { get; private set; }
    public string SelectedDepartmentName { get; private set; } = string.Empty;

    public DeleteDepartmentDialog(IEnumerable<Department> departments)
    {
        InitializeComponent();

        var list = departments.ToList();
        CmbDepartment.ItemsSource = list;
        if (list.Any()) CmbDepartment.SelectedIndex = 0;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (CmbDepartment.SelectedItem is not Department dept)
        {
            TxtError.Text = "Lütfen bir departman seçin.";
            TxtError.Visibility = Visibility.Visible;
            return;
        }

        SelectedDepartmentId = dept.Id;
        SelectedDepartmentName = dept.Name;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
