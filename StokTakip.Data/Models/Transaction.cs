namespace StokTakip.Data.Models;

public enum TransactionType
{
    StockIn,   // Depoya giriş
    StockOut   // Departmana çıkış
}

public class Transaction
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;

    // Nullable: stok GİRİŞLERİ birime değil genel belediye bütçesine yapılır,
    // bu yüzden girişlerde departman yoktur (null). Çıkışlarda zorunludur.
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int Quantity { get; set; }

    // DateTimeOffset: sunucu ve istemci farklı timezone'da olsa bile doğru tarih saklanır
    // DateTime (UTC) kullanılıyor — SQLite EF Core provider DateTimeOffset
    // range sorgularını SQL'e çeviremiyor. UTC saklayıp UI'da local'e çeviriyoruz.
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public TransactionType TransactionType { get; set; }
}
