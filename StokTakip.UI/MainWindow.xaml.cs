using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using StokTakip.Business;
using StokTakip.Data.Models;
using StokTakip.UI.Services;
using StokTakip.UI.ViewModels;
using StokTakip.UI.Views;

namespace StokTakip.UI;

public partial class MainWindow : Window
{
    private readonly IInventoryService _service;
    private List<InventoryItemViewModel> _allItems = new();
    private List<Department> _departments = new();

    // Saat güncelleyici
    private readonly DispatcherTimer _clockTimer;

    public MainWindow(IInventoryService service)
    {
        _service = service;
        InitializeComponent();

        TxtUserName.Text = $"👤 {Environment.UserName}";
        UpdateThemeIcon();

        // Durum çubuğu saati
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => TxtClock.Text = DateTime.Now.ToString("dd.MM.yyyy  HH:mm:ss");
        _clockTimer.Start();
        TxtClock.Text = DateTime.Now.ToString("dd.MM.yyyy  HH:mm:ss");
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        await LoadAllAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // VERİ YÜKLEME
    // ─────────────────────────────────────────────────────────────────

    private async Task LoadAllAsync()
    {
        SetStatus("Veriler yükleniyor...");
        try
        {
            await RefreshDepartmentsAsync();
            await RefreshInventoryAsync();

            DpStart.SelectedDate = DateTime.Today.AddMonths(-1);
            DpEnd.SelectedDate = DateTime.Today;

            SetStatus("Hazır");
        }
        catch (Exception ex)
        {
            SetStatus("Bağlantı hatası!");
            ShowError("Veritabanına bağlanılamadı", ex.Message);
        }
    }

    /// <summary>
    /// Departman listesini yeniden yükler ve rapor ComboBox'ını tazeler.
    /// Mevcut seçim korunur; seçim yoksa ilk departman seçilir.
    /// StockOperationDialog her açılışta _departments'ı aldığı için
    /// yeni eklenen departmanlar orada da otomatik görünür.
    /// </summary>
    private async Task RefreshDepartmentsAsync()
    {
        var selectedId = CmbReportDepartment.SelectedValue as int?;

        _departments = await _service.GetAllDepartmentsAsync();
        CmbReportDepartment.ItemsSource = _departments;

        if (selectedId is int id && _departments.Any(d => d.Id == id))
            CmbReportDepartment.SelectedValue = id;
        else if (_departments.Any())
            CmbReportDepartment.SelectedIndex = 0;
    }

    private async Task RefreshInventoryAsync(string filter = "")
    {
        var items = await _service.GetAllItemsAsync();
        _allItems = items.Select(i => new InventoryItemViewModel(i)).ToList();
        ApplyFilter(filter);
        await RefreshLowStockPanelAsync();
    }

    private void ApplyFilter(string filter)
    {
        DgInventory.ItemsSource = string.IsNullOrWhiteSpace(filter)
            ? _allItems
            : _allItems.Where(i => i.ItemName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                       .ToList();
    }

    private async Task RefreshLowStockPanelAsync()
    {
        var lowStock = await _service.GetLowStockItemsAsync();
        var list = lowStock.ToList();

        if (list.Count > 0)
        {
            PnlLowStock.Visibility = Visibility.Visible;
            LowStockBadge.Visibility = Visibility.Visible;
            TxtBadgeCount.Text = $"{list.Count} kritik ürün";
            LstLowStock.ItemsSource = list;
        }
        else
        {
            PnlLowStock.Visibility = Visibility.Collapsed;
            LowStockBadge.Visibility = Visibility.Collapsed;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // TOOLBAR BUTONLARI
    // ─────────────────────────────────────────────────────────────────

    private async void BtnAddItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddItemDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;

        SetStatus("Ürün ekleniyor...");
        try
        {
            await _service.AddItemAsync(dialog.ItemName, dialog.InitialStock, dialog.MinThreshold);
            await RefreshInventoryAsync(TxtSearch.Text);
            SetStatus($"'{dialog.ItemName}' başarıyla eklendi.");
        }
        catch (InvalidOperationException ex)
        {
            ShowError("Ürün Eklenemedi", ex.Message);
            SetStatus("Hata.");
        }
    }

    private async void BtnAddDepartment_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddDepartmentDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;

        SetStatus("Departman ekleniyor...");
        try
        {
            var dept = await _service.AddDepartmentAsync(dialog.DepartmentName);
            await RefreshDepartmentsAsync();
            CmbReportDepartment.SelectedValue = dept.Id; // yeni ekleneni seç — listeye girdiği görülsün
            SetStatus($"'{dialog.DepartmentName}' departmanı eklendi.");
        }
        catch (InvalidOperationException ex)
        {
            ShowError("Departman Eklenemedi", ex.Message);
            SetStatus("Hata.");
        }
    }

    private async void BtnDeleteDepartment_Click(object sender, RoutedEventArgs e)
    {
        if (!_departments.Any())
        {
            ShowError("Departman Sil", "Silinecek departman yok.");
            return;
        }

        var dialog = new DeleteDepartmentDialog(_departments) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            $"'{dialog.SelectedDepartmentName}' departmanını silmek istediğinizden emin misiniz?",
            "Silme Onayı",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        SetStatus("Departman siliniyor...");
        try
        {
            await _service.DeleteDepartmentAsync(dialog.SelectedDepartmentId);
            await RefreshDepartmentsAsync();
            SetStatus($"'{dialog.SelectedDepartmentName}' departmanı silindi.");
        }
        catch (InvalidOperationException ex)
        {
            // İşlem geçmişi olan departman — servis katmanı korur
            ShowError("Departman Silinemedi", ex.Message);
            SetStatus("Silme engellendi.");
        }
    }

    private async void BtnStockIn_Click(object sender, RoutedEventArgs e)
    {
        if (DgInventory.SelectedItem is not InventoryItemViewModel vm) return;

        var dialog = new StockOperationDialog("StockIn", vm.ItemName, vm.CurrentStock, _departments)
        { Owner = this };
        if (dialog.ShowDialog() != true) return;

        SetStatus("Stok girişi kaydediliyor...");
        var result = await _service.StockInAsync(vm.Id, dialog.Quantity);

        if (result.Success)
        {
            await RefreshInventoryAsync(TxtSearch.Text);
            SetStatus($"{dialog.Quantity} adet '{vm.ItemName}' depoya girişi yapıldı.");
        }
        else
        {
            ShowError("Stok Girişi Başarısız", result.ErrorMessage!);
            SetStatus("İşlem başarısız.");
        }
    }

    private async void BtnStockOut_Click(object sender, RoutedEventArgs e)
    {
        if (DgInventory.SelectedItem is not InventoryItemViewModel vm) return;

        var dialog = new StockOperationDialog("StockOut", vm.ItemName, vm.CurrentStock, _departments)
        { Owner = this };
        if (dialog.ShowDialog() != true) return;

        SetStatus("Stok çıkışı kaydediliyor...");
        var result = await _service.StockOutAsync(vm.Id, dialog.SelectedDepartmentId, dialog.Quantity);

        if (result.Success)
        {
            await RefreshInventoryAsync(TxtSearch.Text);
            SetStatus($"{dialog.Quantity} adet '{vm.ItemName}' çıkışı yapıldı.");
        }
        else
        {
            ShowError("Stok Çıkışı Başarısız", result.ErrorMessage!);
            SetStatus("İşlem başarısız.");
        }
    }

    private async void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (DgInventory.SelectedItem is not InventoryItemViewModel vm) return;

        // PIN Koruması
        var pin = new PinDialog { Owner = this };
        if (pin.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            $"'{vm.ItemName}' ürününü ve tüm işlem geçmişini silmek istediğinizden emin misiniz?\n\nBu işlem geri alınamaz.",
            "Silme Onayı",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        SetStatus("Siliniyor...");
        try
        {
            await _service.DeleteItemAsync(vm.Id);
            await RefreshInventoryAsync(TxtSearch.Text);
            SetStatus($"'{vm.ItemName}' silindi.");
        }
        catch (Exception ex)
        {
            ShowError("Silme Başarısız", ex.Message);
            SetStatus("Hata.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // DATAGRID SEÇİM — Buton Enable/Disable
    // ─────────────────────────────────────────────────────────────────

    private void DgInventory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = DgInventory.SelectedItem != null;
        BtnStockIn.IsEnabled = hasSelection;
        BtnStockOut.IsEnabled = hasSelection;
        BtnDeleteItem.IsEnabled = hasSelection;
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(TxtSearch.Text);

    // ─────────────────────────────────────────────────────────────────
    // RAPORLAR
    // ─────────────────────────────────────────────────────────────────

    private async void BtnReport_Click(object sender, RoutedEventArgs e) => await ShowReportAsync();

    private async Task ShowReportAsync()
    {
        if (CmbReportDepartment.SelectedValue is not int deptId)
        {
            ShowError("Filtre Hatası", "Lütfen bir departman seçin.");
            return;
        }

        if (DpStart.SelectedDate is null || DpEnd.SelectedDate is null)
        {
            ShowError("Filtre Hatası", "Lütfen tarih aralığı seçin.");
            return;
        }

        // Kullanıcı yerel tarih seçer; kayıtlar UTC saklandığı için yerel gün
        // sınırlarını UTC'ye ÇEVİRİYORUZ. (Eskiden yalnızca Kind=Utc işaretleniyor,
        // dönüşüm yapılmıyordu → gün sınırındaki işlemler yanlış güne kayıyordu.)
        var start = DateTime.SpecifyKind(DpStart.SelectedDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
        var end   = DateTime.SpecifyKind(DpEnd.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Local).ToUniversalTime();

        SetStatus("Rapor hazırlanıyor...");
        var transactions = await _service.GetTransactionsByDepartmentAndDateAsync(deptId, start, end);
        var list = transactions.ToList();

        DgReport.ItemsSource = list;

        var totalOut = list.Where(t => t.TransactionType == TransactionType.StockOut).Sum(t => t.Quantity);
        var totalIn  = list.Where(t => t.TransactionType == TransactionType.StockIn).Sum(t => t.Quantity);

        TxtTotalOut.Text = totalOut.ToString("N0") + " adet";
        TxtTotalIn.Text  = totalIn.ToString("N0")  + " adet";
        TxtTxCount.Text  = list.Count.ToString("N0") + " işlem";

        SetStatus($"Rapor hazır — {list.Count} işlem bulundu.");
    }

    // ─────────────────────────────────────────────────────────────────
    // İŞLEM DÜZENLEME — Rapor satırına çift tıkla (aynı-gün kuralı Business'ta)
    // ─────────────────────────────────────────────────────────────────

    private async void DgReport_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgReport.SelectedItem is not Transaction tx) return;

        var typeLabel = tx.TransactionType == TransactionType.StockIn ? "Giriş" : "Çıkış";
        var dialog = new EditTransactionDialog(tx.Item?.ItemName ?? "-", tx.Quantity, typeLabel)
        { Owner = this };
        if (dialog.ShowDialog() != true) return;

        SetStatus("İşlem güncelleniyor...");
        try
        {
            await _service.UpdateTransactionAsync(tx.Id, dialog.NewQuantity);
            await RefreshInventoryAsync(TxtSearch.Text); // düzenleme stoku değiştirmiş olabilir
            await ShowReportAsync();                      // raporu tazele
            SetStatus("İşlem güncellendi.");
        }
        catch (InvalidOperationException ex)
        {
            ShowError("İşlem Düzenlenemedi", ex.Message);
            SetStatus("Hata.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // YAZDIRMA — Önizleme + doğrudan çıktı (ara dosya/dış program gerekmez)
    // ─────────────────────────────────────────────────────────────────

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        // PIN Koruması — Word çıktısıyla aynı politika
        var pin = new PinDialog { Owner = this };
        if (pin.ShowDialog() != true) return;

        if (DgReport.ItemsSource is not List<Transaction> transactions || !transactions.Any())
        {
            ShowError("Yazdırma", "Önce raporu getirin.");
            return;
        }

        var deptName = (CmbReportDepartment.SelectedItem as Department)?.Name ?? "-";

        var preview = new PrintPreviewWindow(deptName, transactions) { Owner = this };
        preview.ShowDialog();
    }

    // ─────────────────────────────────────────────────────────────────
    // WORD FORMU — "Bilgi İşlem Malzeme Alım Formu" (PIN korumalı)
    // ─────────────────────────────────────────────────────────────────

    private void BtnWord_Click(object sender, RoutedEventArgs e)
    {
        // PIN Koruması
        var pin = new PinDialog { Owner = this };
        if (pin.ShowDialog() != true) return;

        if (DgReport.ItemsSource is not List<Transaction> transactions || !transactions.Any())
        {
            ShowError("Word Formu", "Önce raporu getirin.");
            return;
        }

        var deptName = (CmbReportDepartment.SelectedItem as Department)?.Name ?? "-";

        var dlg = new SaveFileDialog
        {
            Filter   = "Word Belgesi (*.docx)|*.docx",
            FileName = $"MalzemeAlimFormu_{DateTime.Now:yyyyMMdd_HHmm}.docx",
            Title    = "Formu Kaydet"
        };

        if (dlg.ShowDialog() != true) return;

        SetStatus("Word formu oluşturuluyor...");
        try
        {
            WordReportExporter.Export(dlg.FileName, deptName, transactions);

            SetStatus($"Word formu kaydedildi: {dlg.FileName}");
            MessageBox.Show("Malzeme alım formu başarıyla kaydedildi.", "Başarılı",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Word Formu Hatası", ex.Message);
            SetStatus("Word hatası.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // YARDIMCI
    // ─────────────────────────────────────────────────────────────────

    private void SetStatus(string message) => TxtStatus.Text = message;

    private static void ShowError(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    // ─────────────────────────────────────────────────────────────────
    // TEMA (AÇIK / KOYU)
    // ─────────────────────────────────────────────────────────────────

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        bool isDark = ThemeManager.Current == ThemeManager.AppTheme.Dark;
        // Koyudayken güneş (aydınlığa dön), açıktayken ay (karanlığa geç)
        BtnTheme.Content = isDark ? "☀" : "🌙";
        BtnTheme.ToolTip = isDark ? "Açık temaya geç" : "Koyu temaya geç";
    }
}