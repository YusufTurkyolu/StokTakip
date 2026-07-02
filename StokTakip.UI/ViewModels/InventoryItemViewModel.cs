namespace StokTakip.UI.ViewModels;

using StokTakip.Data.Models;

/// <summary>
/// InventoryItem için UI-specific wrapper.
/// Model sınıfına UI mantığı eklemek yerine ViewModel kullanırız:
/// 1. IsLowStock gibi hesaplanan özellikler modeli kirletmez
/// 2. DataTrigger binding'leri bu property'ye bağlanır
/// 3. İleride INotifyPropertyChanged eklemek kolaylaşır (MVVM)
/// </summary>
public class InventoryItemViewModel
{
    public int Id { get; }
    public string ItemName { get; }
    public int CurrentStock { get; }
    public int MinimumThreshold { get; }

    // XAML DataTrigger → InventoryRowStyle ve Durum sütunu buna bağlanır
    public bool IsLowStock => CurrentStock < MinimumThreshold;

    // Stok yüzdesi — progress bar veya tooltip için kullanılabilir
    public double StockPercentage => MinimumThreshold == 0 ? 100
        : Math.Min(100, (double)CurrentStock / MinimumThreshold * 100);

    public InventoryItemViewModel(InventoryItem item)
    {
        Id = item.Id;
        ItemName = item.ItemName;
        CurrentStock = item.CurrentStock;
        MinimumThreshold = item.MinimumThreshold;
    }
}
