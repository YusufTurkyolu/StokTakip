using System.IO;
using System.IO.Compression;
using System.Text;
using StokTakip.Data.Models;

namespace StokTakip.UI.Services;

/// <summary>
/// "Bilgi İşlem Malzeme Alım Formu" Word çıktısı.
///
/// Yaklaşım: Templates/MalzemeAlimFormu.docx bir şablondur (kullanıcının verdiği
/// resmî formun birebir kopyası). .docx bir ZIP arşivi olduğu için ek kütüphane
/// gerekmeden açılır, word/document.xml içindeki yer tutucular doldurulur:
///   {{BIRIM}}     → talep eden birim (departman adı)
///   {{OZET}}      → "Aşağıda dökümü verilen N kalem ürün"
///   {{SATIRLAR}}  → işlem dökümü tablo satırları (+ TOPLAM satırı)
/// Formun geri kalanı (başlık, not, ONAY DURUMU imza alanları) şablonda sabittir.
/// </summary>
public static class WordReportExporter
{
    public static void Export(string outputPath, string departmentName, IReadOnlyList<Transaction> transactions)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "MalzemeAlimFormu.docx");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Word şablonu bulunamadı: {templatePath}");

        File.Copy(templatePath, outputPath, overwrite: true);

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var entry = zip.GetEntry("word/document.xml")
            ?? throw new InvalidOperationException("Şablon geçersiz: word/document.xml bulunamadı.");

        string xml;
        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            xml = reader.ReadToEnd();

        int itemCount = transactions.Select(t => t.Item?.ItemName ?? "-").Distinct().Count();

        xml = xml.Replace("{{BIRIM}}", Escape(departmentName));
        xml = xml.Replace("{{OZET}}", Escape($"Aşağıda dökümü verilen {itemCount} kalem ürün"));
        xml = ReplaceMarkerRow(xml, BuildRowsXml(transactions));

        using var stream = entry.Open();
        stream.SetLength(0);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(xml);
    }

    /// <summary>{{SATIRLAR}} işaretçisini taşıyan tablo satırını, üretilen satırlarla değiştirir.</summary>
    private static string ReplaceMarkerRow(string xml, string rowsXml)
    {
        int marker = xml.IndexOf("{{SATIRLAR}}", StringComparison.Ordinal);
        if (marker < 0)
            throw new InvalidOperationException("Şablonda {{SATIRLAR}} işaretçisi bulunamadı.");

        int trStart = xml.LastIndexOf("<w:tr>", marker, StringComparison.Ordinal);
        int trEnd = xml.IndexOf("</w:tr>", marker, StringComparison.Ordinal) + "</w:tr>".Length;
        if (trStart < 0 || trEnd < "</w:tr>".Length)
            throw new InvalidOperationException("Şablon tablo yapısı beklenenden farklı.");

        return xml[..trStart] + rowsXml + xml[trEnd..];
    }

    private static string BuildRowsXml(IReadOnlyList<Transaction> transactions)
    {
        var sb = new StringBuilder();
        int total = 0;

        // Formda kronolojik sıra daha okunaklı (rapor ekranı en yeniden eskiye gösterir)
        foreach (var t in transactions.OrderBy(t => t.TransactionDate))
        {
            total += t.Quantity;
            var date = t.TransactionDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            var item = t.Item?.ItemName ?? "-";
            var type = t.TransactionType == TransactionType.StockIn ? "Giriş" : "Çıkış";

            sb.Append("<w:tr>")
              .Append(Cell(2400, date, center: true))
              .Append(Cell(3672, item, center: false))
              .Append(Cell(1500, type, center: true))
              .Append(Cell(1500, t.Quantity.ToString(), center: true))
              .Append("</w:tr>");
        }

        // TOPLAM satırı
        sb.Append("<w:tr>")
          .Append(Cell(2400, "", center: false))
          .Append(Cell(3672, "", center: false))
          .Append(Cell(1500, "TOPLAM", center: true, bold: true))
          .Append(Cell(1500, total.ToString(), center: true, bold: true))
          .Append("</w:tr>");

        return sb.ToString();
    }

    private static string Cell(int width, string text, bool center, bool bold = false)
    {
        var jc = center ? "<w:jc w:val=\"center\"/>" : "";
        var b = bold ? "<w:b/>" : "";
        var rpr = $"<w:rPr>{b}<w:sz w:val=\"20\"/><w:szCs w:val=\"20\"/></w:rPr>";

        return $"<w:tc><w:tcPr><w:tcW w:w=\"{width}\" w:type=\"dxa\"/></w:tcPr>" +
               $"<w:p><w:pPr>{jc}{rpr}</w:pPr>" +
               $"<w:r>{rpr}<w:t xml:space=\"preserve\">{Escape(text)}</w:t></w:r></w:p></w:tc>";
    }

    private static string Escape(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
