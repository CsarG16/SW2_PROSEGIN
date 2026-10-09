using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Prosegin.Data.Entities;

#pragma warning disable CS0618 // PDFsharp drawing coordinates use point-based unit conversions.

namespace Prosegin.Web.Services;

public static class CotizacionPdfGenerator
{
    private const double Margin = 40;
    private const double PageW = 595.28; // A4 width in points
    private const double TableHeaderHeight = 26;
    private const double ProductRowHeight = 42;

    // Brand colours
    private static readonly XColor Navy      = XColor.FromArgb(11, 23, 39);
    private static readonly XColor Orange    = XColor.FromArgb(238, 145, 28);
    private static readonly XColor LightBg   = XColor.FromArgb(248, 249, 251);
    private static readonly XColor Divider   = XColor.FromArgb(220, 225, 231);
    private static readonly XColor Muted     = XColor.FromArgb(100, 116, 139);
    private static readonly XColor OrangeLight = XColor.FromArgb(255, 237, 213);

    private static readonly CultureInfo PeruCulture = CultureInfo.GetCultureInfo("es-PE");

    static CotizacionPdfGenerator()
    {
        if (OperatingSystem.IsWindows())
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    // ─────────────────────────── Public API ───────────────────────────────

    public static byte[] Generate(
        Cotizacion cotizacion,
        Func<int, string?> technicalSheetUrl,
        string? logoPath = null)
    {
        using var document = new PdfDocument();
        document.Info.Title   = $"Cotización {cotizacion.Correlativo}";
        document.Info.Subject = $"Propuesta comercial para {cotizacion.Cliente.RazonSocial}";
        document.Info.Author  = "Prosegin S.A.C.";

        var items = cotizacion.Detalles.OrderBy(d => d.Id).ToList();

        PdfPage page    = document.AddPage();
        SetPageSize(page);
        XGraphics gfx   = XGraphics.FromPdfPage(page);
        var fonts        = BuildFonts();

        double headerH = DrawHeader(gfx, page, cotizacion, fonts, logoPath);
        DrawClientBlock(gfx, page, cotizacion, fonts, headerH + 8);

        double tableY = headerH + 8 + ClientBlockHeight(cotizacion) + 10;
        DrawTableHeader(gfx, tableY, fonts.Bold8, page);
        double y = tableY + TableHeaderHeight;

        for (int i = 0; i < items.Count; i++)
        {
            var det = items[i];
            if (y + ProductRowHeight > page.Height - 180)
            {
                DrawFooter(gfx, page, fonts.Small, cotizacion.Correlativo);
                gfx.Dispose();

                page = document.AddPage();
                SetPageSize(page);
                gfx = XGraphics.FromPdfPage(page);
                double contHeaderH = DrawHeader(gfx, page, cotizacion, fonts, logoPath);
                DrawClientBlock(gfx, page, cotizacion, fonts, contHeaderH + 8);
                tableY = contHeaderH + 8 + ClientBlockHeight(cotizacion) + 10;
                DrawTableHeader(gfx, tableY, fonts.Bold8, page);
                y = tableY + TableHeaderHeight;
            }

            DrawProductRow(page, gfx, y, i + 1, det, technicalSheetUrl, fonts);
            y += ProductRowHeight;
        }

        // Totals
        if (y + 160 > page.Height - 60)
        {
            DrawFooter(gfx, page, fonts.Small, cotizacion.Correlativo);
            gfx.Dispose();
            page = document.AddPage();
            SetPageSize(page);
            gfx = XGraphics.FromPdfPage(page);
            DrawHeader(gfx, page, cotizacion, fonts, logoPath);
            y = 170;
        }

        DrawTotals(gfx, page, y + 14, cotizacion, fonts);
        DrawConditions(gfx, page, cotizacion, fonts);
        DrawFooter(gfx, page, fonts.Small, cotizacion.Correlativo);
        gfx.Dispose();

        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }

    // ─────────────────────────── Font bundle ─────────────────────────────

    private sealed record FontBundle(
        XFont Body,
        XFont Bold8,
        XFont Bold10,
        XFont Bold14,
        XFont Bold18,
        XFont Small,
        XFont Italic8);

    private static FontBundle BuildFonts() => new(
        Body:   new XFont("Arial", 8,  XFontStyleEx.Regular),
        Bold8:  new XFont("Arial", 8,  XFontStyleEx.Bold),
        Bold10: new XFont("Arial", 10, XFontStyleEx.Bold),
        Bold14: new XFont("Arial", 14, XFontStyleEx.Bold),
        Bold18: new XFont("Arial", 18, XFontStyleEx.Bold),
        Small:  new XFont("Arial", 7,  XFontStyleEx.Regular),
        Italic8:new XFont("Arial", 8,  XFontStyleEx.Italic));

    // ─────────────────────────── Page setup ──────────────────────────────

    private static void SetPageSize(PdfPage page)
    {
        page.Width  = XUnit.FromMillimeter(210);
        page.Height = XUnit.FromMillimeter(297);
    }

    // ─────────────────────────── Header ──────────────────────────────────

    private static double DrawHeader(XGraphics gfx, PdfPage page, Cotizacion cot, FontBundle f, string? logoPath)
    {
        const double headerH = 70;

        // Navy background strip
        gfx.DrawRectangle(new XSolidBrush(Navy), 0, 0, page.Width, headerH);

        // Orange accent bar on left
        gfx.DrawRectangle(new XSolidBrush(Orange), 0, 0, 6, headerH);

        // Logo
        bool logoDrawn = false;
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            try
            {
                using var img = XImage.FromFile(logoPath);
                double maxLogoW = 160;
                double maxLogoH = 44;
                double ratio = Math.Min(maxLogoW / img.PixelWidth, maxLogoH / img.PixelHeight);
                double lw = img.PixelWidth  * ratio;
                double lh = img.PixelHeight * ratio;
                gfx.DrawImage(img, Margin, (headerH - lh) / 2, lw, lh);
                logoDrawn = true;
            }
            catch { /* fall back to text */ }
        }

        if (!logoDrawn)
        {
            // Fallback: text logo
            gfx.DrawString("PROSEGIN", f.Bold18, XBrushes.White,
                new XRect(Margin, 18, 200, 28), XStringFormats.TopLeft);
            gfx.DrawString("Equipos de Protección Personal", f.Body,
                new XSolidBrush(XColor.FromArgb(180, 200, 220)),
                new XRect(Margin, 46, 240, 14), XStringFormats.TopLeft);
        }

        // "COTIZACIÓN" label right side
        gfx.DrawString("COTIZACIÓN", f.Bold18, XBrushes.White,
            new XRect(page.Width - Margin - 220, 14, 220, 24), XStringFormats.TopRight);

        // Correlativo badge
        double badgeW = 140, badgeH = 22;
        double badgeX = page.Width - Margin - badgeW;
        double badgeY = 42;
        gfx.DrawRoundedRectangle(new XSolidBrush(Orange), badgeX, badgeY, badgeW, badgeH, 5, 5);
        gfx.DrawString(cot.Correlativo, f.Bold10, XBrushes.White,
            new XRect(badgeX, badgeY + 3, badgeW, badgeH - 4), XStringFormats.Center);

        return headerH;
    }

    // ─────────────────────────── Client block ────────────────────────────

    private static double ClientBlockHeight(Cotizacion cot)
    {
        // Row 1: CLIENTE + EMISIÓN header = 52
        // Row 2: address/contact line = 30
        // Row 3: ubigeo/correo line   = 26 (optional)
        bool hasUbigeo = !string.IsNullOrWhiteSpace(cot.Cliente.Distrito);
        bool hasEmail  = !string.IsNullOrWhiteSpace(cot.Cliente.CorreoElectronico);
        return (hasUbigeo || hasEmail) ? 112 : 86;
    }

    private static void DrawClientBlock(XGraphics gfx, PdfPage page, Cotizacion cot, FontBundle f, double top)
    {
        var cliente = cot.Cliente;
        double w = page.Width - Margin * 2;

        // Background card
        gfx.DrawRoundedRectangle(new XSolidBrush(LightBg), Margin, top, w, ClientBlockHeight(cot), 4, 4);
        // Left orange accent
        gfx.DrawRoundedRectangle(new XSolidBrush(Orange), Margin, top, 4, ClientBlockHeight(cot), 2, 2);

        double lx = Margin + 14;
        double rx = page.Width - Margin - 4;

        // ── Row 1: Razón Social + fecha / condición ──
        double r1y = top + 10;

        // Label CLIENTE
        DrawMiniLabel(gfx, "CLIENTE", lx, r1y, f.Small);
        gfx.DrawString(cliente.RazonSocial, f.Bold10, new XSolidBrush(Navy),
            new XRect(lx, r1y + 10, w * 0.55, 14), XStringFormats.TopLeft);

        // Dates block on right
        double dateColW = 110;
        DrawInfoPair(gfx, "FECHA DE EMISIÓN",
            cot.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy", PeruCulture),
            rx - dateColW * 2 - 12, r1y, dateColW, f);

        DrawInfoPair(gfx, "CONDICIÓN DE PAGO",
            cot.CondicionPago,
            rx - dateColW, r1y, dateColW, f);

        // ── Row 2: RUC + dirección + teléfono ──
        double r2y = r1y + 36;
        gfx.DrawLine(new XPen(Divider), lx, r2y - 4, page.Width - Margin - 14, r2y - 4);

        double col1w = 80, col2w = w * 0.42, col3w = 90;
        DrawInfoPair(gfx, "RUC", cliente.Ruc, lx, r2y, col1w, f);
        DrawInfoPair(gfx, "DIRECCIÓN FISCAL", cliente.DireccionFiscal, lx + col1w + 10, r2y, col2w, f);
        if (!string.IsNullOrWhiteSpace(cliente.Telefono))
            DrawInfoPair(gfx, "TELÉFONO", cliente.Telefono, lx + col1w + 10 + col2w + 8, r2y, col3w, f);

        // ── Row 3 (optional): Distrito / Dpto + Correo ──
        bool hasUbigeo = !string.IsNullOrWhiteSpace(cliente.Distrito);
        bool hasEmail  = !string.IsNullOrWhiteSpace(cliente.CorreoElectronico);

        if (hasUbigeo || hasEmail)
        {
            double r3y = r2y + 30;
            gfx.DrawLine(new XPen(Divider), lx, r3y - 4, page.Width - Margin - 14, r3y - 4);

            if (hasUbigeo)
            {
                var ubigeoStr = string.Join(", ",
                    new[] { cliente.Distrito, cliente.Provincia, cliente.Departamento }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                DrawInfoPair(gfx, "UBICACIÓN", ubigeoStr, lx, r3y, w * 0.5, f);
            }

            if (hasEmail)
                DrawInfoPair(gfx, "CORREO ELECTRÓNICO", cliente.CorreoElectronico,
                    hasUbigeo ? lx + w * 0.5 + 10 : lx, r3y, w * 0.45, f);
        }
    }

    private static void DrawInfoPair(XGraphics gfx, string label, string value, double x, double y, double maxW, FontBundle f)
    {
        DrawMiniLabel(gfx, label, x, y, f.Small);
        // Truncate if too long
        gfx.DrawString(TruncateText(value, maxW, f.Body), f.Bold8, new XSolidBrush(Navy),
            new XRect(x, y + 10, maxW, 14), XStringFormats.TopLeft);
    }

    private static void DrawMiniLabel(XGraphics gfx, string label, double x, double y, XFont font)
        => gfx.DrawString(label, font, new XSolidBrush(Muted), new XRect(x, y, 200, 10), XStringFormats.TopLeft);

    // ─────────────────────────── Table ───────────────────────────────────

    // Column widths: #, SKU, Descripción, Cant., P.Unit., Subtotal, Ficha
    private static readonly double[] ColW = { 22, 64, 158, 40, 74, 82, 62 };

    private static double TableWidth => ColW.Sum();

    private static void DrawTableHeader(XGraphics gfx, double y, XFont font, PdfPage page)
    {
        var labels = new[] { "#", "SKU", "DESCRIPCIÓN", "CANT.", "P. UNIT.", "SUBTOTAL", "FICHA" };
        gfx.DrawRectangle(new XSolidBrush(Navy), Margin, y, TableWidth, TableHeaderHeight);
        // Orange left bar on header
        gfx.DrawRectangle(new XSolidBrush(Orange), Margin, y, 4, TableHeaderHeight);

        double x = Margin + 4;
        for (int i = 0; i < labels.Length; i++)
        {
            gfx.DrawString(labels[i], font, XBrushes.White,
                new XRect(x + 4, y + 7, ColW[i] - 8, 14), XStringFormats.TopLeft);
            x += ColW[i];
        }
    }

    private static void DrawProductRow(
        PdfPage page,
        XGraphics gfx,
        double y,
        int lineNum,
        CotizacionDetalle det,
        Func<int, string?> fichaUrl,
        FontBundle f)
    {
        bool isEven = lineNum % 2 == 0;
        if (isEven)
            gfx.DrawRectangle(new XSolidBrush(LightBg), Margin, y, TableWidth, ProductRowHeight);

        gfx.DrawLine(new XPen(Divider), Margin, y + ProductRowHeight, Margin + TableWidth, y + ProductRowHeight);

        bool hasFicha = !string.IsNullOrWhiteSpace(det.Producto.RutaFichaTecnicaPdf);
        var values = new[]
        {
            lineNum.ToString(CultureInfo.InvariantCulture),
            det.Producto.Sku,
            det.Producto.Nombre,
            det.Cantidad.ToString("N0", PeruCulture),
            FormatMoney(det.PrecioVentaCalculado),
            FormatMoney(det.Subtotal),
            hasFicha ? "Ver ficha ↗" : "—"
        };

        double x = Margin + 4;
        for (int i = 0; i < ColW.Length; i++)
        {
            double cx = x + 4;
            double cy = y + 8;
            double cw = ColW[i] - 8;

            if (i == 0)
            {
                // Row number circle
                gfx.DrawEllipse(new XSolidBrush(OrangeLight), cx, cy, 16, 16);
                gfx.DrawString(values[i], f.Small, new XSolidBrush(Orange),
                    new XRect(cx, cy + 2, 16, 14), XStringFormats.Center);
            }
            else if (i == 1)
            {
                gfx.DrawString(values[i], f.Bold8, new XSolidBrush(Navy),
                    new XRect(cx, cy, cw, 14), XStringFormats.TopLeft);
            }
            else if (i == 2)
            {
                gfx.DrawString(TruncateText(values[i], cw, f.Body), f.Body, new XSolidBrush(Navy),
                    new XRect(cx, cy, cw, 14), XStringFormats.TopLeft);
                gfx.DrawString($"U.M.: {det.Producto.UnidadMedida}", f.Small, new XSolidBrush(Muted),
                    new XRect(cx, cy + 17, cw, 12), XStringFormats.TopLeft);
            }
            else if (i == ColW.Length - 1 && hasFicha)
            {
                // Ficha link styled
                gfx.DrawString(values[i], f.Small, new XSolidBrush(Orange),
                    new XRect(cx, cy + 4, cw, 14), XStringFormats.TopLeft);
                var url = fichaUrl(det.ProductoId);
                if (!string.IsNullOrWhiteSpace(url))
                    page.AddWebLink(new PdfRectangle(new XRect(cx, cy + 2, cw, 18)), url);
            }
            else
            {
                gfx.DrawString(values[i], f.Body, new XSolidBrush(Navy),
                    new XRect(cx, cy + 4, cw, 14), XStringFormats.TopLeft);
            }

            x += ColW[i];
        }
    }

    // ─────────────────────────── Totals ──────────────────────────────────

    private static void DrawTotals(XGraphics gfx, PdfPage page, double y, Cotizacion cot, FontBundle f)
    {
        double panelW = 210;
        double panelX = page.Width - Margin - panelW;

        // Subtotal row
        DrawTotalRow(gfx, "Subtotal (sin IGV)", FormatMoney(cot.Subtotal), panelX, y, panelW, f, subtle: true);
        y += 22;
        DrawTotalRow(gfx, "IGV 18%", FormatMoney(cot.Igv), panelX, y, panelW, f, subtle: true);
        y += 22;

        // Separator
        gfx.DrawLine(new XPen(Orange, 1.5), panelX, y, panelX + panelW, y);
        y += 8;

        // Total box
        gfx.DrawRoundedRectangle(new XSolidBrush(Navy), panelX, y, panelW, 36, 4, 4);
        gfx.DrawRoundedRectangle(new XSolidBrush(Orange), panelX, y, 4, 36, 2, 2);
        gfx.DrawString("TOTAL A PAGAR", f.Bold10, XBrushes.White,
            new XRect(panelX + 12, y + 10, panelW * 0.5, 18), XStringFormats.TopLeft);
        gfx.DrawString($"{FormatMoney(cot.Total)} PEN", f.Bold10, XBrushes.White,
            new XRect(panelX + 12, y + 10, panelW - 16, 18), XStringFormats.TopRight);
        y += 48;

        // Amount in words
        gfx.DrawString($"Son: {AmountInWords(cot.Total)}", f.Italic8, new XSolidBrush(Muted),
            new XRect(Margin, y, page.Width - Margin * 2, 16), XStringFormats.TopLeft);
    }

    private static void DrawTotalRow(XGraphics gfx, string label, string value, double x, double y, double w, FontBundle f, bool subtle)
    {
        gfx.DrawString(label, f.Body, new XSolidBrush(subtle ? Muted : Navy),
            new XRect(x, y, w * 0.55, 16), XStringFormats.TopLeft);
        gfx.DrawString(value, f.Bold8, new XSolidBrush(Navy),
            new XRect(x, y, w - 2, 16), XStringFormats.TopRight);
    }

    // ─────────────────────────── Conditions ──────────────────────────────

    private static void DrawConditions(XGraphics gfx, PdfPage page, Cotizacion cot, FontBundle f)
    {
        double panelH = 46;
        double panelY = page.Height - Margin - 48 - panelH;

        gfx.DrawRoundedRectangle(new XSolidBrush(LightBg), Margin, panelY, page.Width - Margin * 2, panelH, 4, 4);
        gfx.DrawRoundedRectangle(new XSolidBrush(Orange), Margin, panelY, 4, panelH, 2, 2);

        double lx = Margin + 14;
        gfx.DrawString("CONDICIONES COMERCIALES", f.Bold8, new XSolidBrush(Navy),
            new XRect(lx, panelY + 8, 200, 14), XStringFormats.TopLeft);

        string condText = $"• Pago: {cot.CondicionPago}   • Moneda: PEN (Soles)   • Validez: 48 horas desde la emisión   • Precios incluyen IGV (18%)";
        gfx.DrawString(condText, f.Small, new XSolidBrush(Muted),
            new XRect(lx, panelY + 24, page.Width - Margin * 2 - 20, 14), XStringFormats.TopLeft);
    }

    // ─────────────────────────── Footer ──────────────────────────────────

    private static void DrawFooter(XGraphics gfx, PdfPage page, XFont font, string correlativo)
    {
        double fy = page.Height - Margin;
        // Orange top line
        gfx.DrawLine(new XPen(Orange, 1), Margin, fy - 24, page.Width - Margin, fy - 24);

        gfx.DrawString("Prosegin S.A.C. – Equipos de Protección Personal | Las Malvinas, Lima, Perú",
            font, new XSolidBrush(Muted),
            new XRect(Margin, fy - 18, page.Width - Margin * 2, 12), XStringFormats.TopLeft);

        gfx.DrawString(correlativo, font, new XSolidBrush(Muted),
            new XRect(Margin, fy - 18, page.Width - Margin * 2, 12), XStringFormats.TopRight);
    }

    // ─────────────────────────── Helpers ─────────────────────────────────

    private static string TruncateText(string text, double maxW, XFont font)
    {
        // Rough estimate: avg char ~5.5 points wide at 8pt
        int maxChars = (int)(maxW / 5.5);
        if (text.Length <= maxChars) return text;
        return text[..(maxChars - 1)] + "…";
    }

    private static string FormatMoney(decimal amount) => $"S/ {amount.ToString("N2", PeruCulture)}";

    private static string AmountInWords(decimal amount)
    {
        var abs = Math.Abs(amount);
        var whole = decimal.ToInt64(decimal.Truncate(abs));
        var cents = decimal.ToInt32(decimal.Round((abs - whole) * 100m, 0, MidpointRounding.AwayFromZero));
        if (cents == 100) { whole++; cents = 0; }
        var prefix = amount < 0 ? "Menos " : string.Empty;
        return $"{prefix}{NumberInWords(whole)} soles con {cents:00}/100";
    }

    private static string NumberInWords(long number)
    {
        if (number == 0) return "cero";
        var groups  = new List<string>();
        var scales  = new[] { "", "mil", "millón", "mil millones", "billón", "mil billones", "trillón" };
        var groupIndex = 0;
        while (number > 0)
        {
            var group = (int)(number % 1000);
            if (group > 0)
            {
                var words = ThreeDigitNumberInWords(group);
                if (groupIndex == 1 && group == 1)
                    groups.Add("mil");
                else if (groupIndex > 0)
                {
                    var scale = scales[groupIndex];
                    if (group == 1 && groupIndex == 2)
                        groups.Add($"un {scale}");
                    else
                    {
                        if (groupIndex == 2 && group > 1) scale = "millones";
                        groups.Add($"{ApocopateOne(words)} {scale}");
                    }
                }
                else
                    groups.Add(words);
            }
            number /= 1000;
            groupIndex++;
        }
        groups.Reverse();
        return string.Join(" ", groups);
    }

    private static string ThreeDigitNumberInWords(int number)
    {
        var units    = new[] { "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve" };
        var tens     = new[] { "", "diez", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };
        var hundreds = new[] { "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos" };

        if (number == 100) return "cien";
        var words = new List<string>();
        if (number >= 100) { words.Add(hundreds[number / 100]); number %= 100; }

        if      (number is >= 10 and <= 15) words.Add(new[] { "diez", "once", "doce", "trece", "catorce", "quince" }[number - 10]);
        else if (number is >= 16 and <= 19) words.Add($"dieci{units[number - 10]}");
        else if (number is >= 21 and <= 29) words.Add($"veinti{units[number - 20]}");
        else if (number >= 20)
        {
            var rem = number % 10;
            words.Add(rem == 0 ? tens[number / 10] : $"{tens[number / 10]} y {units[rem]}");
        }
        else if (number > 0) words.Add(units[number]);

        return string.Join(" ", words);
    }

    private static string ApocopateOne(string words) =>
        words.EndsWith("veintiuno", StringComparison.Ordinal) ? $"{words[..^3]}ún"  :
        words.EndsWith("uno",       StringComparison.Ordinal) ? $"{words[..^3]}un"  : words;
}

#pragma warning restore CS0618
