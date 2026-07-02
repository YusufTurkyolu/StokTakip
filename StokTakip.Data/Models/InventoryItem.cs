using System.ComponentModel.DataAnnotations;

namespace StokTakip.Data.Models;

public class InventoryItem
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    // ConcurrencyCheck → EF, UPDATE sorgusuna WHERE CurrentStock = @original ekler.
    // İki kullanıcı aynı anda stok düşürürse biri DbUpdateConcurrencyException alır.
    [ConcurrencyCheck]
    public int CurrentStock { get; set; }

    public int MinimumThreshold { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
