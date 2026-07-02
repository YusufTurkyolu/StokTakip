using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using StokTakip.Data.Models;
using StokTakip.UI.Services;

namespace StokTakip.UI.Views;

public partial class PrintPreviewWindow : Window
{
    private readonly string _departmentName;
    private readonly IReadOnlyList<Transaction> _transactions;

    public PrintPreviewWindow(string departmentName, IReadOnlyList<Transaction> transactions)
    {
        InitializeComponent();

        _departmentName = departmentName;
        _transactions = transactions;

        TxtInfo.Text = $"Birim: {departmentName}   •   {transactions.Count} işlem";
        Viewer.Document = PrintFormBuilder.Build(departmentName, transactions);
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return;

        // Önizlemedeki belge viewer'a bağlı; yazdırmaya taze bir kopya verilir
        // (aynı FlowDocument iki yerde birden kullanılamaz).
        var doc = PrintFormBuilder.Build(_departmentName, _transactions);
        dlg.PrintDocument(
            ((IDocumentPaginatorSource)doc).DocumentPaginator,
            "Bilgi İşlem Malzeme Alım Formu");
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
