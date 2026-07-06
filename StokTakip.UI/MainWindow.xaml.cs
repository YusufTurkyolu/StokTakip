using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    // Toast bildirim zamanlayıcı
    private readonly DispatcherTimer _toastTimer;

    // Rapor türü RadioButton'ları XAML parse edilirken Checked olayını tetikler;
    // o an henüz oluşmamış kontrollere erişmemek için hazır olana kadar handler'ı atlarız.
    private bool _reportUiReady;

    public MainWindow(IInventoryService service)
    {
        _service = service;
        InitializeComponent();
        _reportUiReady = true;

        TxtUserName.Text = $"👤 {Environment.UserName}";
        UpdateThemeIcon();

        // Durum çubuğu saati
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => TxtClock.Text = DateTime.Now.ToString("dd.MM.yyyy  HH:mm:ss");
        _clockTimer.Start();
        TxtClock.Text = DateTime.Now.ToString("dd.MM.yyyy  HH:mm:ss");

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) => HideToast();
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

            DpStart.SelectedDate = DateTime.Today.AddDays(-7); // varsayılan: 1 hafta öncesi
            DpEnd.SelectedDate = DateTime.Today;               // varsayılan: bugün

            SetStatus("Hazır");
        }
        catch (Exception ex)
        {
            SetStatus("Bağlantı hatası!");
            ShowToast($"Veritabanına bağlanılamadı: {ex.Message}", ToastType.Error);
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

        // Ürün bazlı rapor ComboBox'ını da tazele (mevcut seçim korunur)
        var selectedItemId = CmbReportItem.SelectedValue as int?;
        CmbReportItem.ItemsSource = _allItems;
        if (selectedItemId is int iid && _allItems.Any(i => i.Id == iid))
            CmbReportItem.SelectedValue = iid;
        else if (_allItems.Any())
            CmbReportItem.SelectedIndex = 0;

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

    /// <summary>
    /// Elle yenileme — program birden çok bilgisayardan aynı veritabanını kullandığı
    /// için diğer kullanıcıların işlemleri ancak yenilemeyle bu ekrana yansır.
    /// </summary>
    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("Yenileniyor...");
        try
        {
            await RefreshDepartmentsAsync();
            await RefreshInventoryAsync(TxtSearch.Text);
            ShowToast("Liste güncellendi.", ToastType.Info);
        }
        catch (Exception ex)
        {
            ShowToast($"Yenileme başarısız: {ex.Message}", ToastType.Error);
        }
    }

    private async void BtnAddItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddItemDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;

        SetStatus("Ürün ekleniyor...");
        try
        {
            await _service.AddItemAsync(dialog.ItemName, dialog.InitialStock, dialog.MinThreshold);
            await RefreshInventoryAsync(TxtSearch.Text);
            ShowToast($"'{dialog.ItemName}' başarıyla eklendi.", ToastType.Success);
        }
        catch (InvalidOperationException ex)
        {
            ShowToast($"Ürün eklenemedi: {ex.Message}", ToastType.Error);
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
            ShowToast($"'{dialog.DepartmentName}' departmanı eklendi.", ToastType.Success);
        }
        catch (InvalidOperationException ex)
        {
            ShowToast($"Departman eklenemedi: {ex.Message}", ToastType.Error);
        }
    }

    private async void BtnDeleteDepartment_Click(object sender, RoutedEventArgs e)
    {
        if (!_departments.Any())
        {
            ShowToast("Silinecek departman yok.", ToastType.Warning);
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
            ShowToast($"'{dialog.SelectedDepartmentName}' departmanı silindi.", ToastType.Success);
        }
        catch (InvalidOperationException ex)
        {
            // İşlem geçmişi olan departman — servis katmanı korur
            ShowToast($"Departman silinemedi: {ex.Message}", ToastType.Error);
        }
    }

    private async void BtnStockIn_Click(object sender, RoutedEventArgs e)
    {
        if (DgInventory.SelectedItem is not InventoryItemViewModel vm)
        {
            ShowToast("Önce listeden bir ürün seçin.", ToastType.Warning);
            return;
        }

        try
        {
            var dialog = new StockOperationDialog("StockIn", vm.ItemName, vm.CurrentStock, _departments)
            { Owner = this };
            if (dialog.ShowDialog() != true) return;

            SetStatus("Stok girişi kaydediliyor...");
            var result = await _service.StockInAsync(vm.Id, dialog.Quantity);

            if (result.Success)
            {
                await RefreshInventoryAsync(TxtSearch.Text);
                ShowToast($"{dialog.Quantity} adet '{vm.ItemName}' depoya girişi yapıldı.", ToastType.Success);
            }
            else
            {
                ShowToast($"Stok girişi başarısız: {result.ErrorMessage}", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Stok girişi hatası: {ex.Message}", ToastType.Error);
            SetStatus("Hata.");
        }
    }

    private async void BtnStockOut_Click(object sender, RoutedEventArgs e)
    {
        if (DgInventory.SelectedItem is not InventoryItemViewModel vm)
        {
            ShowToast("Önce listeden bir ürün seçin.", ToastType.Warning);
            return;
        }

        try
        {
            var dialog = new StockOperationDialog("StockOut", vm.ItemName, vm.CurrentStock, _departments)
            { Owner = this };
            if (dialog.ShowDialog() != true) return;

            SetStatus("Stok çıkışı kaydediliyor...");
            var result = await _service.StockOutAsync(vm.Id, dialog.SelectedDepartmentId, dialog.Quantity);

            if (result.Success)
            {
                await RefreshInventoryAsync(TxtSearch.Text);
                ShowToast($"{dialog.Quantity} adet '{vm.ItemName}' çıkışı yapıldı.", ToastType.Success);
            }
            else
            {
                ShowToast($"Stok çıkışı başarısız: {result.ErrorMessage}", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Stok çıkışı hatası: {ex.Message}", ToastType.Error);
            SetStatus("Hata.");
        }
    }

    private async void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (DgInventory.SelectedItem is not InventoryItemViewModel vm) return;

        var confirm = MessageBox.Show(
            $"'{vm.ItemName}' ürününü silmek istediğinizden emin misiniz?",
            "Silme Onayı",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        SetStatus("Siliniyor...");
        try
        {
            await _service.DeleteItemAsync(vm.Id);
            await RefreshInventoryAsync(TxtSearch.Text);
            ShowToast($"'{vm.ItemName}' başarıyla silindi.", ToastType.Success);
        }
        catch (InvalidOperationException ex)
        {
            // İşlem geçmişi olan ürün — servis katmanı korur
            ShowToast($"Ürün silinemedi: {ex.Message}", ToastType.Error);
        }
        catch (Exception ex)
        {
            ShowToast($"Silme başarısız: {ex.Message}", ToastType.Error);
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

    /// <summary>
    /// Rapor türü değiştiğinde (Departmana Göre / Ürüne Göre) filtre alanlarını,
    /// tablo sütunlarını ve butonları uygun moda çevirir.
    /// </summary>
    private void ReportMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_reportUiReady) return; // XAML parse anındaki ilk tetiklemeyi atla

        bool byItem = RbByItem.IsChecked == true;

        PnlDepartmentFilter.Visibility = byItem ? Visibility.Collapsed : Visibility.Visible;
        PnlItemFilter.Visibility       = byItem ? Visibility.Visible   : Visibility.Collapsed;

        // Tabloda ürün modunda "Departman" sütunu, departman modunda "Ürün" sütunu
        ColReportItem.Visibility       = byItem ? Visibility.Collapsed : Visibility.Visible;
        ColReportDepartment.Visibility = byItem ? Visibility.Visible   : Visibility.Collapsed;

        // Word/Yazdır çıktısı departman talep formudur; ürün modunda anlamlı değil
        BtnWord.IsEnabled  = !byItem;
        BtnPrint.IsEnabled = !byItem;

        // Önceki modun sonuçları karışmasın
        DgReport.ItemsSource = null;
        TxtTotalOut.Text = "—";
        TxtTotalIn.Text  = "—";
        TxtTxCount.Text  = "—";
        SetStatus(byItem ? "Ürün bazlı rapor modu." : "Departman bazlı rapor modu.");
    }

    private async Task ShowReportAsync()
    {
        if (DpStart.SelectedDate is null || DpEnd.SelectedDate is null)
        {
            ShowToast("Lütfen tarih aralığı seçin.", ToastType.Warning);
            return;
        }

        if (DpStart.SelectedDate.Value.Date > DpEnd.SelectedDate.Value.Date)
        {
            ShowToast("Başlangıç tarihi, bitiş tarihinden sonra olamaz.", ToastType.Warning);
            return;
        }

        // Kullanıcı yerel tarih seçer; kayıtlar UTC saklandığı için yerel gün
        // sınırlarını UTC'ye ÇEVİRİYORUZ. (Eskiden yalnızca Kind=Utc işaretleniyor,
        // dönüşüm yapılmıyordu → gün sınırındaki işlemler yanlış güne kayıyordu.)
        var start = DateTime.SpecifyKind(DpStart.SelectedDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
        var end   = DateTime.SpecifyKind(DpEnd.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Local).ToUniversalTime();

        List<Transaction> list;

        if (RbByItem.IsChecked == true)
        {
            // Ürün bazlı: seçili ürün hangi departmanlara gitti / ne zaman girdi
            if (CmbReportItem.SelectedValue is not int itemId)
            {
                ShowToast("Lütfen bir ürün seçin.", ToastType.Warning);
                return;
            }
            SetStatus("Rapor hazırlanıyor...");
            list = (await _service.GetTransactionsByItemAndDateAsync(itemId, start, end)).ToList();
        }
        else
        {
            // Departman bazlı: seçili departmana giden ürünler
            if (CmbReportDepartment.SelectedValue is not int deptId)
            {
                ShowToast("Lütfen bir departman seçin.", ToastType.Warning);
                return;
            }
            SetStatus("Rapor hazırlanıyor...");
            list = (await _service.GetTransactionsByDepartmentAndDateAsync(deptId, start, end)).ToList();
        }

        DgReport.ItemsSource = list;

        var totalOut = list.Where(t => t.TransactionType == TransactionType.StockOut).Sum(t => t.Quantity);
        var totalIn  = list.Where(t => t.TransactionType == TransactionType.StockIn).Sum(t => t.Quantity);

        TxtTotalOut.Text = totalOut.ToString("N0") + " adet";
        TxtTotalIn.Text  = totalIn.ToString("N0")  + " adet";
        TxtTxCount.Text  = list.Count.ToString("N0") + " işlem";

        ShowToast($"Rapor hazır — {list.Count} işlem bulundu.", ToastType.Info);
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
            ShowToast("İşlem başarıyla güncellendi.", ToastType.Success);
        }
        catch (InvalidOperationException ex)
        {
            ShowToast($"İşlem düzenlenemedi: {ex.Message}", ToastType.Error);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // İŞLEM SİLME — Seçili veya tümü (stok bakiyeleri otomatik geri alınır)
    // ─────────────────────────────────────────────────────────────────

    private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = DgReport.SelectedItems.Cast<Transaction>().ToList();
        if (selected.Count == 0)
        {
            ShowToast("Lütfen silmek istediğiniz işlemleri seçin.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"{selected.Count} adet işlemi silmek istediğinizden emin misiniz?\n\n" +
            "Stok bakiyeleri otomatik olarak geri alınacaktır.\nBu işlem geri alınamaz.",
            "İşlem Silme Onayı",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        await DeleteTransactionsAsync(selected.Select(t => t.Id));
    }

    private async void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
    {
        if (DgReport.ItemsSource is not List<Transaction> transactions || !transactions.Any())
        {
            ShowToast("Silinecek işlem yok. Önce raporu getirin.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Rapordaki {transactions.Count} işlemin tamamını silmek istediğinizden emin misiniz?\n\n" +
            "Stok bakiyeleri otomatik olarak geri alınacaktır.\nBu işlem geri alınamaz.",
            "Toplu Silme Onayı",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        await DeleteTransactionsAsync(transactions.Select(t => t.Id));
    }

    private async Task DeleteTransactionsAsync(IEnumerable<int> ids)
    {
        SetStatus("İşlemler siliniyor...");
        try
        {
            // Kim sildiğini denetim log'una yazmak için Windows kullanıcı adı gönderilir
            var count = await _service.DeleteTransactionsAsync(ids, Environment.UserName);
            await RefreshInventoryAsync(TxtSearch.Text);
            await ShowReportAsync();
            ShowToast($"{count} işlem başarıyla silindi.", ToastType.Success);
        }
        catch (InvalidOperationException ex)
        {
            ShowToast($"Silme başarısız: {ex.Message}", ToastType.Error);
        }
        catch (Exception ex)
        {
            ShowToast($"Hata: {ex.Message}", ToastType.Error);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // YAZDIRMA — Önizleme + doğrudan çıktı (ara dosya/dış program gerekmez)
    // ─────────────────────────────────────────────────────────────────

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (DgReport.ItemsSource is not List<Transaction> transactions || !transactions.Any())
        {
            ShowToast("Önce raporu getirin.", ToastType.Warning);
            return;
        }

        var deptName = (CmbReportDepartment.SelectedItem as Department)?.Name ?? "-";

        var preview = new PrintPreviewWindow(deptName, transactions) { Owner = this };
        preview.ShowDialog();
    }

    // ─────────────────────────────────────────────────────────────────
    // WORD FORMU — "Bilgi İşlem Malzeme Alım Formu"
    // ─────────────────────────────────────────────────────────────────

    private void BtnWord_Click(object sender, RoutedEventArgs e)
    {
        if (DgReport.ItemsSource is not List<Transaction> transactions || !transactions.Any())
        {
            ShowToast("Önce raporu getirin.", ToastType.Warning);
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
            ShowToast("Malzeme alım formu başarıyla kaydedildi.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Word formu hatası: {ex.Message}", ToastType.Error);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // YARDIMCI
    // ─────────────────────────────────────────────────────────────────

    private void SetStatus(string message) => TxtStatus.Text = message;

    // ─────────────────────────────────────────────────────────────────
    // TOAST BİLDİRİM SİSTEMİ
    // ─────────────────────────────────────────────────────────────────

    private enum ToastType { Success, Error, Warning, Info }

    private void ShowToast(string message, ToastType type)
    {
        // Zamanlayıcıyı sıfırla (üst üste gelen bildirimler için)
        _toastTimer.Stop();

        // Tür bazlı ikon ve renk
        (string icon, Color color) = type switch
        {
            ToastType.Success => ("✓", (Color)ColorConverter.ConvertFromString("#27AE60")),
            ToastType.Error   => ("✕", (Color)ColorConverter.ConvertFromString("#C0392B")),
            ToastType.Warning => ("⚠", (Color)ColorConverter.ConvertFromString("#E67E22")),
            ToastType.Info    => ("ℹ", (Color)ColorConverter.ConvertFromString("#2E6DA4")),
            _                 => ("ℹ", (Color)ColorConverter.ConvertFromString("#2E6DA4"))
        };

        ToastIcon.Text = icon;
        ToastMessage.Text = message;
        ToastPanel.Background = new SolidColorBrush(color);

        // Durum çubuğunu da güncelle (kalıcı referans)
        SetStatus(message);

        // Giriş animasyonu: aşağıdan kayarak gelir + fade in
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

        var slideIn = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

        ToastPanel.BeginAnimation(OpacityProperty, fadeIn);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

        _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer.Stop();

        // Çıkış animasyonu: yukarı kayarak kaybolur + fade out
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };

        var slideOut = new DoubleAnimation(0, -15, TimeSpan.FromMilliseconds(300))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };

        ToastPanel.BeginAnimation(OpacityProperty, fadeOut);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);
    }

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