using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StokTakip.Business;
using StokTakip.Data;

namespace StokTakip.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // DI Container kur — WPF'de Microsoft.Extensions.DependencyInjection ile
        // ASP.NET Core'daki aynı DI altyapısını kullanırız.
        // Bu sayede servisler test edilebilir ve decoupled olur.
        var services = new ServiceCollection();

        // IDbContextFactory — thread-safe context yönetimi.
        // DB yolu MUTLAK: exe'nin yanındaki stok.db her zaman kullanılır.
        // (Göreli yol, uygulama farklı bir çalışma diziniyle başlatıldığında
        // başka klasörde ikinci bir veritabanı oluşmasına yol açıyordu.)
        // Üretime geçişte bu satırı SQL Server connection string ile değiştir:
        // options.UseSqlServer("Server=...;Database=StokTakipDB;Trusted_Connection=True;")
        var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "stok.db");
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}")
        );

        // Singleton: servis durumsuzdur (yalnızca singleton IDbContextFactory'yi tutar,
        // her işlemde yeni context açar). WPF'de DI scope'u olmadığı için Scoped zaten
        // pratikte singleton gibi davranırdı; niyeti açıkça belirtiyoruz.
        services.AddSingleton<IInventoryService, InventoryService>();
        services.AddTransient<MainWindow>();

        Services = services.BuildServiceProvider();

        // Migration'ları uygula — veritabanı yoksa oluşturur, varsa günceller
        // Seed data da bu adımda eklenir
        try
        {
            var factory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Veritabanı başlatılamadı:\n\n{ex.Message}",
                "Kritik Hata",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Kayıtlı tema tercihini (açık/koyu) pencere gösterilmeden önce uygula
        ThemeManager.Initialize();

        // Ana pencereyi DI üzerinden oluştur (constructor injection çalışır)
        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
