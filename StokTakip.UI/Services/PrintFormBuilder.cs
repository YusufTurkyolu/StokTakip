using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using StokTakip.Data.Models;

namespace StokTakip.UI.Services;

/// <summary>
/// "Bilgi İşlem Malzeme Alım Formu"nun yazdırılabilir (FlowDocument) hali.
/// Word şablonuyla aynı düzeni WPF'in kendi yazdırma altyapısı için üretir:
/// dış program veya ara dosya gerekmeden önizleme + doğrudan yazıcı çıktısı alınır.
/// Renkler bilinçli olarak sabittir (siyah/beyaz) — çıktı uygulama temasından etkilenmez.
/// </summary>
public static class PrintFormBuilder
{
    // Word puntosu → WPF DIP (1pt = 96/72 DIP)
    private static double Pt(double points) => points * 96.0 / 72.0;

    public static FlowDocument Build(string departmentName, IReadOnlyList<Transaction> transactions)
    {
        var doc = new FlowDocument
        {
            PageWidth   = 793.7,               // A4 genişlik: 21,0 cm
            PageHeight  = 1122.5,              // A4 yükseklik: 29,7 cm
            PagePadding = new Thickness(94.5), // 2,5 cm kenar boşluğu (şablonla aynı)
            ColumnWidth = double.PositiveInfinity,
            FontFamily  = new FontFamily("Calibri"),
            FontSize    = Pt(11),
            Background  = Brushes.White,
            Foreground  = Brushes.Black
        };

        // ── Başlık ──
        doc.Blocks.Add(new Paragraph(new Run("BİLGİ İŞLEM MALZEME ALIM FORMU"))
        {
            FontSize = Pt(20),
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, Pt(18))
        });

        // ── Form alanları ──
        int itemCount = transactions.Select(t => t.Item?.ItemName ?? "-").Distinct().Count();

        doc.Blocks.Add(Field("Talep eden Birim       :", departmentName, bottom: 14));
        doc.Blocks.Add(Field("Talep Konusu Ürün   :", $"Aşağıda dökümü verilen {itemCount} kalem ürün", bottom: 6));
        doc.Blocks.Add(Field("Tahmini Maliyet        :", "", bottom: 16)); // elle doldurulacak

        // ── İşlem dökümü ──
        doc.Blocks.Add(new Paragraph(new Run("İŞLEM DÖKÜMÜ"))
        {
            FontSize = Pt(12),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        doc.Blocks.Add(BuildTable(transactions));

        // ── Not ──
        doc.Blocks.Add(new Paragraph(new Run(
            "Not : Bilgi İşlem servisinden :  Talep edilen tüm ürün ve hizmet tutarları için , " +
            "başkan yardımcısının onayladığına dair imza alınarak talepte bulunulacaktır."))
        {
            FontSize = Pt(9),
            Margin = new Thickness(0, 10, 0, 14)
        });

        // ── Onay bölümü (imza alanları — şablonla birebir) ──
        doc.Blocks.Add(new Paragraph(new Run("ONAY DURUMU")
        {
            TextDecorations = TextDecorations.Underline
        })
        {
            FontSize = Pt(20),
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, Pt(16))
        });

        // İki müdür yan yana (kenarlıksız tablo)
        var sig = new Table { CellSpacing = 0, FontSize = Pt(14), FontWeight = FontWeights.Bold };
        sig.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        sig.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var birimP = new Paragraph { TextAlignment = TextAlignment.Center };
        birimP.Inlines.Add(new Run("Talep Eden") { TextDecorations = TextDecorations.Underline });
        birimP.Inlines.Add(new Run(" Birim Müdürü"));

        var destekP = new Paragraph(new Run("Destek Hizmetleri Müdürü"))
        {
            TextAlignment = TextAlignment.Center
        };

        var sigRow = new TableRow();
        sigRow.Cells.Add(new TableCell(birimP));
        sigRow.Cells.Add(new TableCell(destekP));

        var sigGroup = new TableRowGroup();
        sigGroup.Rows.Add(sigRow);
        sig.RowGroups.Add(sigGroup);
        doc.Blocks.Add(sig);

        // İmza boşluğu + Başkan Yardımcısı
        doc.Blocks.Add(new Paragraph(new Run("Başkan Yardımcısı"))
        {
            FontSize = Pt(14),
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, Pt(44), 0, Pt(24)) // üstteki boşluk = müdür imza alanı
        });

        return doc;
    }

    private static Paragraph Field(string label, string value, double bottom)
    {
        var p = new Paragraph { FontSize = Pt(16), Margin = new Thickness(0, 0, 0, bottom) };
        p.Inlines.Add(new Run(label) { FontWeight = FontWeights.Bold });
        if (!string.IsNullOrEmpty(value))
            p.Inlines.Add(new Run(" " + value));
        return p;
    }

    private static Table BuildTable(IReadOnlyList<Transaction> transactions)
    {
        var table = new Table
        {
            CellSpacing = 0,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.75, 0.75, 0, 0), // dış üst+sol; hücreler alt+sağ çizer
            FontSize = Pt(10)
        };

        // Word şablonuyla aynı oranlar (2400/3672/1500/1500), SABİT genişlik olarak.
        // Not: FlowDocument tablosunda Star genişlikler güvenilir uygulanmıyor
        // (sütunlar tek kolona çöküyor) — pixel genişlik şart.
        // Toplam = A4 içerik genişliği: 793,7 − 2×94,5 = 604,7
        table.Columns.Add(new TableColumn { Width = new GridLength(160,   GridUnitType.Pixel) }); // Tarih
        table.Columns.Add(new TableColumn { Width = new GridLength(244.7, GridUnitType.Pixel) }); // Ürün
        table.Columns.Add(new TableColumn { Width = new GridLength(100,   GridUnitType.Pixel) }); // İşlem
        table.Columns.Add(new TableColumn { Width = new GridLength(100,   GridUnitType.Pixel) }); // Miktar

        var group = new TableRowGroup();

        // Başlık satırı
        var header = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9)) };
        header.Cells.Add(Cell("Tarih", center: true, bold: true));
        header.Cells.Add(Cell("Ürün", center: true, bold: true));
        header.Cells.Add(Cell("İşlem", center: true, bold: true));
        header.Cells.Add(Cell("Miktar", center: true, bold: true));
        group.Rows.Add(header);

        // Veri satırları — formda kronolojik sıra daha okunaklı
        int total = 0;
        foreach (var t in transactions.OrderBy(t => t.TransactionDate))
        {
            total += t.Quantity;
            var row = new TableRow();
            row.Cells.Add(Cell(t.TransactionDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), center: true));
            row.Cells.Add(Cell(t.Item?.ItemName ?? "-", center: false));
            row.Cells.Add(Cell(t.TransactionType == TransactionType.StockIn ? "Giriş" : "Çıkış", center: true));
            row.Cells.Add(Cell(t.Quantity.ToString(), center: true));
            group.Rows.Add(row);
        }

        // TOPLAM satırı
        var totalRow = new TableRow();
        totalRow.Cells.Add(Cell("", center: false));
        totalRow.Cells.Add(Cell("", center: false));
        totalRow.Cells.Add(Cell("TOPLAM", center: true, bold: true));
        totalRow.Cells.Add(Cell(total.ToString(), center: true, bold: true));
        group.Rows.Add(totalRow);

        table.RowGroups.Add(group);
        return table;
    }

    private static TableCell Cell(string text, bool center, bool bold = false) =>
        new(new Paragraph(new Run(text))
        {
            TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
        })
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 0, 0.75, 0.75),
            Padding = new Thickness(5, 3, 5, 3)
        };
}
