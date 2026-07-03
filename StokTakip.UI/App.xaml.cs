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
        //
        // DAĞITIM MODELİ: exe sunucudaki paylaşılan bir klasöre kurulur, belediyedeki
        // bilgisayarlar programı bu paylaşım üzerinden (UNC yolu) çalıştırır.
        // DB yolu MUTLAK ve exe'nin yanındadır: böylece tüm istemciler sunucudaki
        // AYNI stok.db dosyasını kullanır — herkes aynı veriyi görür.
        //
        // Çok kullanıcılı erişim güvenceleri:
        //   - CurrentStock [ConcurrencyCheck] + retry (InventoryService) → veri tutarlılığı
        //   - Microsoft.Data.Sqlite varsayılan 30 sn busy-timeout → "database is locked"
        //     yerine kilidin açılması beklenir
        //   - journal_mode=DELETE (aşağıda) → WAL modu ağ paylaşımında birden çok
        //     makineyle ÇALIŞMAZ (paylaşılan bellek ister); DELETE modu SMB'de güvenlidir
        //
        // Kullanıcı sayısı artar ve kilitlenme sorunları görülürse SQL Server Express'e
        // geçiş için bu satırı değiştirmek yeterli:
        // options.UseSqlServer("Server=SUNUCU;Database=StokTakipDB;Trusted_Connection=True;")
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

        // Migration'ları uygula — veritabanı yoksa oluşturur, varsa günceller.
        // Birden çok istemci aynı anda açılırsa SQLite kilidi ikinciyi bekletir;
        // migration'lar zaten uygulanmışsa ikinci istemci hiçbir şey yapmaz.
        try
        {
            var factory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.Migrate();

            // Ağ paylaşımı güvencesi: WAL modu tek makinede hızlıdır ama SMB üzerinden
            // birden çok makine erişince bozulmaya yol açar (paylaşılan bellek gerektirir).
            // DB dosyası daha önce WAL'e alınmış olsa bile burada DELETE moduna sabitlenir.
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
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
