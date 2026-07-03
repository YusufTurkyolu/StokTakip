using System.IO;
using System.Windows;

namespace StokTakip.UI;

/// <summary>
/// Uygulama temasını (açık/koyu) çalışma anında değiştirir.
/// Renkler Themes/LightTheme.xaml ve Themes/DarkTheme.xaml içinde aynı anahtarlarla
/// tanımlıdır; kontroller bunlara DynamicResource ile bağlandığı için sözlük
/// değiştirildiği an tüm arayüz yeni renklere geçer.
/// Seçilen tema KULLANICININ KENDİ profil klasörüne (%LocalAppData%\StokTakip)
/// yazılır: program sunucudaki paylaşımdan çalıştığı için exe klasörü tüm
/// kullanıcılarda ortaktır — oraya yazılsaydı herkes birbirinin temasını değiştirir,
/// salt-okunur paylaşımda ise kayıt hiç yapılamazdı.
/// </summary>
public static class ThemeManager
{
    public enum AppTheme { Light, Dark }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StokTakip", "theme.setting");

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    /// <summary>Kayıtlı tercihi okuyup uygular. Uygulama açılışında bir kez çağrılır.</summary>
    public static void Initialize()
    {
        var theme = AppTheme.Light;
        try
        {
            if (File.Exists(SettingsPath) &&
                Enum.TryParse(File.ReadAllText(SettingsPath).Trim(), out AppTheme saved))
                theme = saved;
        }
        catch { /* ayar okunamazsa varsayılan (açık) tema */ }

        Apply(theme);
    }

    public static void Toggle()
        => Apply(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

    public static void Apply(AppTheme theme)
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/{theme}Theme.xaml", UriKind.Relative)
        };

        // İlk birleştirilmiş sözlük tema sözlüğüdür (App.xaml'da böyle sıralandı) —
        // yerine yenisini koyunca DynamicResource bağlamaları anında güncellenir.
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0)
            merged[0] = dict;
        else
            merged.Add(dict);

        Current = theme;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, theme.ToString());
        }
        catch { /* kaydedilemezse tercih bu oturumda geçerli olur */ }
    }
}
