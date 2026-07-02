using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StokTakip.Data;

/// <summary>
/// EF Core CLI (dotnet ef migrations add) bu factory'yi kullanır.
/// Migration aracı App.xaml.cs'deki DI container'ı çalıştıramaz.
/// Bu class, design-time'da DbContext'i doğrudan oluşturur.
/// Sadece migration araçları için — production kodu bunu kullanmaz.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=stok.db")
            .Options;

        return new AppDbContext(opts);
    }
}
