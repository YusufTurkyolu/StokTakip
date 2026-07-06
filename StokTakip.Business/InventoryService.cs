using Microsoft.EntityFrameworkCore;
using StokTakip.Data;
using StokTakip.Data.Models;

namespace StokTakip.Business;

public class InventoryService : IInventoryService
{
    // IDbContextFactory: WPF'de async/await farklı thread'lerden çağrı gelebilir.
    // DbContext thread-safe değildir. Factory her operasyon için yeni instance verir.
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public InventoryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<InventoryItem>> GetAllItemsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.InventoryItems
            .OrderBy(i => i.ItemName)
            .ToListAsync();
    }

    public async Task<List<Department>> GetAllDepartmentsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Departments
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<Department> AddDepartmentAsync(string name)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var trimmedName = name.Trim();

        // Duplicate kontrolü — case-insensitive (AddItemAsync ile aynı yaklaşım)
        var existing = await db.Departments
            .FirstOrDefaultAsync(d => d.Name.ToLower() == trimmedName.ToLower());

        if (existing != null)
            throw new InvalidOperationException($"'{trimmedName}' adlı departman zaten kayıtlı.");

        var department = new Department { Name = trimmedName };
        db.Departments.Add(department);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index ihlali: check-then-act aralığında başka kullanıcı
            // aynı adı eklemiş olabilir. DB son savunma hattıdır.
            throw new InvalidOperationException($"'{trimmedName}' adlı departman zaten kayıtlı.");
        }

        return department;
    }

    /// <summary>
    /// Departman silme — yalnızca hiç işlem görmemiş departmanlar silinebilir.
    /// Amaç: yanlış girilen bir adın düzeltilmesi. İşlem geçmişi olan departman
    /// silinseydi cascade tüm giriş/çıkış kayıtlarını da silerdi; buna izin vermiyoruz.
    /// </summary>
    public async Task<bool> DeleteDepartmentAsync(int departmentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var dept = await db.Departments.FindAsync(departmentId);
        if (dept == null) return false;

        var hasTransactions = await db.Transactions.AnyAsync(t => t.DepartmentId == departmentId);
        if (hasTransactions)
            throw new InvalidOperationException(
                $"'{dept.Name}' departmanına ait işlem kayıtları olduğu için silinemez.\n" +
                "Yalnızca hiç işlem görmemiş (yanlışlıkla eklenmiş) departmanlar silinebilir.");

        db.Departments.Remove(dept);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<InventoryItem> AddItemAsync(string itemName, int initialStock, int minThreshold)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var trimmedName = itemName.Trim();

        // Duplicate kontrolü — case-insensitive
        var existing = await db.InventoryItems
            .FirstOrDefaultAsync(i => i.ItemName.ToLower() == trimmedName.ToLower());

        if (existing != null)
            throw new InvalidOperationException($"'{trimmedName}' adlı ürün zaten kayıtlı.");

        var item = new InventoryItem
        {
            ItemName = trimmedName,
            CurrentStock = initialStock,
            MinimumThreshold = minThreshold
        };

        db.InventoryItems.Add(item);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index ihlali: yukarıdaki check-then-act kontrolü ile SaveChanges
            // arasında başka bir kullanıcı aynı adı eklemiş olabilir (race condition),
            // ya da NOCASE collation farklı bir büyük/küçük harf varyantını yakalar.
            // DB son savunma hattıdır; kullanıcıya anlaşılır mesaja çeviriyoruz.
            throw new InvalidOperationException($"'{trimmedName}' adlı ürün zaten kayıtlı.");
        }

        return item;
    }

    /// <summary>
    /// Stok Girişi — StockOut ile aynı optimistic concurrency korumasını uygular.
    /// CurrentStock [ConcurrencyCheck] taşıdığı için iki eşzamanlı giriş çakışabilir;
    /// çakışma olursa retry edilir. Sözleşme StockOut ile tutarlıdır (Result döner).
    /// Departman parametresi yoktur: girişler genel belediye bütçesinden yapılır,
    /// kayıt DepartmentId=null ile atılır.
    /// </summary>
    public async Task<StockOperationResult> StockInAsync(int itemId, int quantity)
    {
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var db = await _contextFactory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            try
            {
                var item = await db.InventoryItems.FindAsync(itemId)
                    ?? throw new InvalidOperationException("Ürün bulunamadı.");

                item.CurrentStock += quantity;

                db.Transactions.Add(new Transaction
                {
                    ItemId = itemId,
                    DepartmentId = null, // giriş: genel bütçe, birime bağlı değil
                    Quantity = quantity,
                    TransactionDate = DateTime.UtcNow,
                    TransactionType = TransactionType.StockIn
                });

                await db.SaveChangesAsync(); // ConcurrencyCheck burada devreye girer
                await tx.CommitAsync();
                return new StockOperationResult(true);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                if (attempt == maxRetries - 1)
                    return new StockOperationResult(false,
                        "Eşzamanlılık çakışması: Başka bir kullanıcı aynı ürünü güncelledi.\nLütfen tekrar deneyin.");

                await Task.Delay(150 * (attempt + 1)); // Exponential backoff
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new StockOperationResult(false, $"Veritabanı hatası: {ex.Message}");
            }
        }

        return new StockOperationResult(false, "İşlem tamamlanamadı. Lütfen tekrar deneyin.");
    }

    /// <summary>
    /// Optimistic Concurrency ile Güvenli Stok Düşme:
    ///
    /// Senaryo: A ve B kullanıcısı aynı anda 50'şer adet düşmek istiyor, stokta 60 var.
    ///   1) A okur: CurrentStock=60
    ///   2) B okur: CurrentStock=60
    ///   3) A yazar: UPDATE ... WHERE CurrentStock=60 → Başarılı, stok=10
    ///   4) B yazar: UPDATE ... WHERE CurrentStock=60 → WHERE sağlanmaz, 0 satır → Exception
    ///   5) B'ye hata döner, retry yapılır; yeni stok=10, talep=50 → "Yetersiz stok" mesajı
    /// </summary>
    public async Task<StockOperationResult> StockOutAsync(int itemId, int departmentId, int quantity)
    {
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var db = await _contextFactory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            try
            {
                var item = await db.InventoryItems.FindAsync(itemId)
                    ?? throw new InvalidOperationException("Ürün bulunamadı.");

                if (item.CurrentStock - quantity < 0)
                    return new StockOperationResult(false,
                        $"Yetersiz stok!\nMevcut: {item.CurrentStock} adet  |  İstenen: {quantity} adet");

                item.CurrentStock -= quantity;

                db.Transactions.Add(new Transaction
                {
                    ItemId = itemId,
                    DepartmentId = departmentId,
                    Quantity = quantity,
                    TransactionDate = DateTime.UtcNow,
                    TransactionType = TransactionType.StockOut
                });

                await db.SaveChangesAsync(); // ConcurrencyCheck burada devreye girer
                await tx.CommitAsync();
                return new StockOperationResult(true);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                if (attempt == maxRetries - 1)
                    return new StockOperationResult(false,
                        "Eşzamanlılık çakışması: Başka bir kullanıcı aynı ürünü güncelledi.\nLütfen tekrar deneyin.");

                await Task.Delay(150 * (attempt + 1)); // Exponential backoff
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new StockOperationResult(false, $"Veritabanı hatası: {ex.Message}");
            }
        }

        return new StockOperationResult(false, "İşlem tamamlanamadı. Lütfen tekrar deneyin.");
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.InventoryItems
            .Where(i => i.CurrentStock < i.MinimumThreshold)
            .OrderBy(i => i.CurrentStock)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetTransactionsByDepartmentAndDateAsync(
        int departmentId, DateTime startDate, DateTime endDate)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Transactions
            .Include(t => t.Item)
            .Include(t => t.Department)
            .Where(t => t.DepartmentId == departmentId &&
                        t.TransactionDate >= startDate &&
                        t.TransactionDate <= endDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    /// <summary>
    /// Ürün bazlı rapor: belirli bir ürünün verilen tarih aralığındaki tüm
    /// hareketlerini döner. Çıkışlarda Department dolu (hangi departmana gitti),
    /// girişlerde Department null'dur (genel bütçe). UI bunu "Depo Girişi" gösterir.
    /// </summary>
    public async Task<List<Transaction>> GetTransactionsByItemAndDateAsync(
        int itemId, DateTime startDate, DateTime endDate)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Transactions
            .Include(t => t.Item)
            .Include(t => t.Department)
            .Where(t => t.ItemId == itemId &&
                        t.TransactionDate >= startDate &&
                        t.TransactionDate <= endDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    /// <summary>
    /// Aynı-Gün Düzenleme Kuralı:
    /// İş kuralları Business katmanında yaşar — UI sadece gösterim yapar.
    /// UI'da butonu disable etmek kullanıcı deneyimi içindir, güvenlik için değil.
    /// Gerçek kural burada uygulanır.
    /// </summary>
    public async Task<bool> UpdateTransactionAsync(int transactionId, int newQuantity)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var transaction = await db.Transactions
            .Include(t => t.Item)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null) return false;

        // Tarih kontrolü — UTC normalize ederek karşılaştır
        if (transaction.TransactionDate.Date != DateTime.UtcNow.Date)
            throw new InvalidOperationException(
                "Geçmiş tarihli işlemler düzenlenemez.\nSadece bugün yapılan işlemler değiştirilebilir.");

        int diff = newQuantity - transaction.Quantity;

        if (transaction.TransactionType == TransactionType.StockOut)
        {
            if (transaction.Item.CurrentStock - diff < 0)
                throw new InvalidOperationException("Bu düzenleme stoku negatife düşürür.");
            transaction.Item.CurrentStock -= diff;
        }
        else
        {
            transaction.Item.CurrentStock += diff;
        }

        transaction.Quantity = newQuantity;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Ürün silme — yalnızca hiç işlem görmemiş ürünler silinebilir (DeleteDepartmentAsync
    /// ile aynı yaklaşım). İşlem geçmişi olan ürün silinseydi cascade tüm giriş/çıkış
    /// kayıtlarını da silerdi; stok geçmişinin kalıcı kaybını önlemek için buna izin vermiyoruz.
    /// </summary>
    public async Task<bool> DeleteItemAsync(int itemId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var item = await db.InventoryItems.FindAsync(itemId);
        if (item == null) return false;

        var hasTransactions = await db.Transactions.AnyAsync(t => t.ItemId == itemId);
        if (hasTransactions)
            throw new InvalidOperationException(
                $"'{item.ItemName}' ürününe ait işlem kayıtları olduğu için silinemez.\n" +
                "Yalnızca hiç işlem görmemiş (yanlışlıkla eklenmiş) ürünler silinebilir.");

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Seçilen işlemleri siler ve stok bakiyelerini geri alır.
    /// Çıkış silindiyse stok artar, giriş silindiyse azalır.
    /// Silinmeden önce her kaydın bir kopyası DeletedTransactionLogs tablosuna
    /// (kim, ne zaman, ne sildi) yazılır — asıl kayıt kalıcı silinse de denetim
    /// izi kalır. Tüm işlem tek DB transaction'ında yapılır — ya hepsi ya hiçbiri.
    /// </summary>
    public async Task<int> DeleteTransactionsAsync(IEnumerable<int> transactionIds, string deletedBy)
    {
        var ids = transactionIds.ToList();
        if (ids.Count == 0) return 0;

        await using var db = await _contextFactory.CreateDbContextAsync();
        await using var dbTx = await db.Database.BeginTransactionAsync();

        try
        {
            var transactions = await db.Transactions
                .Include(t => t.Item)
                .Include(t => t.Department)
                .Where(t => ids.Contains(t.Id))
                .ToListAsync();

            if (transactions.Count == 0) return 0;

            var deletedAt = DateTime.UtcNow;

            foreach (var tx in transactions)
            {
                if (tx.Item == null) continue;

                // Stok geri alımı: çıkış silindiyse stok geri eklenir, giriş silindiyse düşülür
                if (tx.TransactionType == TransactionType.StockOut)
                {
                    tx.Item.CurrentStock += tx.Quantity;
                }
                else // StockIn
                {
                    if (tx.Item.CurrentStock - tx.Quantity < 0)
                        throw new InvalidOperationException(
                            $"'{tx.Item.ItemName}' ürününün giriş kaydı silinemez: stok negatife düşer.\n" +
                            $"Mevcut: {tx.Item.CurrentStock} adet, Giriş miktarı: {tx.Quantity} adet");

                    tx.Item.CurrentStock -= tx.Quantity;
                }

                db.DeletedTransactionLogs.Add(new DeletedTransactionLog
                {
                    OriginalTransactionId = tx.Id,
                    ItemName = tx.Item.ItemName,
                    DepartmentName = tx.Department?.Name,
                    Quantity = tx.Quantity,
                    TransactionType = tx.TransactionType,
                    OriginalTransactionDate = tx.TransactionDate,
                    DeletedBy = deletedBy,
                    DeletedAt = deletedAt
                });
            }

            db.Transactions.RemoveRange(transactions);
            await db.SaveChangesAsync();
            await dbTx.CommitAsync();

            return transactions.Count;
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }
    }
}
