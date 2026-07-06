namespace StokTakip.Data.Models;

// Silinen işlemlerin kalıcı denetim kaydı. Alanlar bilinçli olarak DENORMALIZE:
// FK ile Item/Department'a bağlanmaz, çünkü orijinal ürün veya departman ileride
// silinse bile (örn. tüm geçmişi silindikten sonra ürün de kaldırılırsa) bu kayıt
// hâlâ okunabilir kalsın isteniyor. Bir denetimde "kim, ne zaman, neyi sildi"
// sorusuna DB dosyası üzerinden (örn. DB Browser for SQLite ile) cevap verilebilir.
public class DeletedTransactionLog
{
    public int Id { get; set; }

    public int OriginalTransactionId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public int Quantity { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime OriginalTransactionDate { get; set; }

    public string DeletedBy { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
}
