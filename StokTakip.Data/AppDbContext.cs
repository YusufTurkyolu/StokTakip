using Microsoft.EntityFrameworkCore;
using StokTakip.Data.Models;

namespace StokTakip.Data;

public class AppDbContext : DbContext
{
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // İsim kirliliğini DB katmanında da engelle.
        // NOCASE collation → unique index büyük/küçük harf duyarsız olur
        // ("A4 Kağıt" ile "a4 kağıt" çakışır). Not: SQLite NOCASE yalnızca ASCII
        // A-Z harflerini katlar; Türkçe'ye özgü İ/ı için tam çözüm değildir ama
        // varsayılan (tamamen case-sensitive) davranışa göre belirgin iyileşmedir.
        modelBuilder.Entity<InventoryItem>()
            .Property(i => i.ItemName)
            .UseCollation("NOCASE");

        modelBuilder.Entity<InventoryItem>()
            .HasIndex(i => i.ItemName)
            .IsUnique();

        // Departman adları da benzersiz olsun — ürün adıyla aynı yaklaşım
        // (NOCASE collation + unique index). Departmanlar artık UI'dan
        // eklenebildiği için DB katmanında da koruma şart.
        modelBuilder.Entity<Department>()
            .Property(d => d.Name)
            .UseCollation("NOCASE");

        modelBuilder.Entity<Department>()
            .HasIndex(d => d.Name)
            .IsUnique();

        // TransactionType enum'u okunabilir string olarak sakla (int değil)
        // SQL'e bakan biri "0" yerine "StockOut" görür
        modelBuilder.Entity<Transaction>()
            .Property(t => t.TransactionType)
            .HasConversion<string>();

        // Seed data bilinçli olarak YOK: program boş başlar, ürünler ve
        // departmanlar kullanıcı tarafından uygulama içinden eklenir.
        // (Eski demo kayıtları RemoveSeedData migration'ı ile kaldırıldı.)
    }
}
