using System;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Tesseract;
using System.Drawing.Drawing2D;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

public static class OcrHelper
{
    // İsteğe bağlı basit kontrast/binarize – OCR'ı iyileştirir
    private static Bitmap Preprocess(Bitmap src)
    {
        // 2x büyüt (Tesseract 300+ DPI seviyor)
        var scaled = new Bitmap(src.Width * 2, src.Height * 2, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, scaled.Width, scaled.Height);
        }

        // Basit gri + threshold (siyah yazı/beyaz zemin)
        var bmp = new Bitmap(scaled.Width, scaled.Height, PixelFormat.Format24bppRgb);
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = scaled.GetPixel(x, y);
                int gray = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
                bmp.SetPixel(x, y, gray > 170 ? Color.White : Color.Black);
            }
        scaled.Dispose();
        return bmp;
    }

    public static DataTable ExtractTimesFromImage(string imagePath, Rectangle? roi = null)
    {
        var tessDir = Path.Combine(AppContext.BaseDirectory, "tessdata");
        using var full = new Bitmap(imagePath);
        using var crop = roi.HasValue ? full.Clone(roi.Value, PixelFormat.Format24bppRgb) : new Bitmap(full);
        using var pre = Preprocess(crop);

        using var engine = new TesseractEngine(tessDir, "eng", EngineMode.Default);
        engine.SetVariable("tessedit_char_whitelist", "0123456789:");
        engine.SetVariable("classify_bln_numeric_mode", "1");     // rakam ağırlığı
        engine.DefaultPageSegMode = PageSegMode.SparseText;       // sütunda dağınık kısa metinler için

        // temp png
        string tmp = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}.png");
        pre.Save(tmp, ImageFormat.Png);
        using var pix = Pix.LoadFromFile(tmp);

        string text;
        using (var page = engine.Process(pix, PageSegMode.SparseText))
            text = page.GetText() ?? string.Empty;

        try { File.Delete(tmp); } catch { }

        // Olası yanlış eşleşmeleri normalize et
        text = text.Replace('O', '0').Replace('o', '0').Replace('S', '5').Replace('l', '1').Replace('|', '1');

        var dt = new DataTable();
        dt.Columns.Add("Harcanan", typeof(string));
        foreach (Match m in Regex.Matches(text, @"\b\d{2}:\d{2}:\d{2}\b"))
            dt.Rows.Add(m.Value);

        return dt;
    }
}
