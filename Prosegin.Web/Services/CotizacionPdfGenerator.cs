using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Prosegin.Data.Entities;

#pragma warning disable CS0618 // PDFsharp drawing coordinates use point-based unit conversions.

namespace Prosegin.Web.Services;

public static class CotizacionPdfGenerator
{
    private const double Margin = 42;
    private const double TableHeaderHeight = 25;
    private const double ProductRowHeight = 40;
    private static readonly XColor Navy = XColor.FromArgb(11, 23, 39);
    private static readonly XColor Orange = XColor.FromArgb(238, 145, 28);
    private static readonly XColor Muted = XColor.FromArgb(91, 105, 120);
    private static readonly CultureInfo PeruCulture = CultureInfo.GetCultureInfo("es-PE");

    static CotizacionPdfGenerator()
    {
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    public static byte[] Generate(Cotizacion cotizacion, Func<int, string?> technicalSheetUrl)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Cotización {cotizacion.Correlativo}";
        document.Info.Subject = $"Propuesta comercial para {cotizacion.Cliente.RazonSocial}";

        var items = cotizacion.Detalles.OrderBy(detalle => detalle.Id).ToList();
        PdfPage page = document.AddPage();
        SetPageSize(page);
        var graphics = XGraphics.FromPdfPage(page);
        var bodyFont = new XFont("Arial", 8, XFontStyleEx.Regular);
        var smallFont = new XFont("Arial", 7, XFontStyleEx.Regular);
        var boldFont = new XFont("Arial", 8, XFontStyleEx.Bold);
        var headingFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var titleFont = new XFont("Arial", 10, XFontStyleEx.Bold);

        DrawBrand(graphics, page, cotizacion, headingFont, titleFont);
        double y = 174;
        DrawTableHeader(graphics, y, titleFont);
        y += TableHeaderHeight;

        for (var index = 0; index < items.Count; index++)
        {
            var detalle = items[index];
            if (y + ProductRowHeight > page.Height - 165)
            {
                graphics.Dispose();
                page = document.AddPage();
                SetPageSize(page);
                graphics = XGraphics.FromPdfPage(page);
                DrawBrand(graphics, page, cotizacion, headingFont, titleFont);
                y = 174;
                DrawTableHeader(graphics, y, titleFont);
                y += TableHeaderHeight;
            }

            DrawProductRow(page, graphics, y, index + 1, detalle, technicalSheetUrl, bodyFont, boldFont, smallFont);
            y += ProductRowHeight;
        }

        if (y + 145 > page.Height - 42)
        {
            graphics.Dispose();
            page = document.AddPage();
            SetPageSize(page);
            graphics = XGraphics.FromPdfPage(page);
            DrawBrand(graphics, page, cotizacion, headingFont, titleFont);
            y = 174;
        }

        DrawTotals(graphics, page, y + 12, cotizacion, bodyFont, boldFont);
        graphics.Dispose();

        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private static void SetPageSize(PdfPage page)
    {
        page.Width = XUnit.FromMillimeter(210);
        page.Height = XUnit.FromMillimeter(297);
    }

    private static void DrawBrand(
        XGraphics graphics,
        PdfPage page,
        Cotizacion cotizacion,
        XFont headingFont,
        XFont titleFont)
    {
        var bodyFont = new XFont("Arial", 8, XFontStyleEx.Regular);
        graphics.DrawRectangle(new XSolidBrush(Orange), Margin, 34, 4, 36);
        graphics.DrawString("PROSEGIN", new XFont("Arial", 14, XFontStyleEx.Bold),
            new XSolidBrush(Navy), new XRect(Margin + 13, 32, 170, 22), XStringFormats.TopLeft);
        graphics.DrawString("Equipos de protección personal", bodyFont, new XSolidBrush(Muted),
            new XRect(Margin + 13, 53, 220, 14), XStringFormats.TopLeft);
        graphics.DrawString("COTIZACIÓN", headingFont, new XSolidBrush(Navy),
            new XRect(page.Width - 220, 34, 178, 24), XStringFormats.TopRight);
        graphics.DrawString(cotizacion.Correlativo, titleFont, new XSolidBrush(Muted),
            new XRect(page.Width - 220, 59, 178, 16), XStringFormats.TopRight);

        var boxY = 85;
        graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(245, 247, 250)),
            Margin, boxY, page.Width - Margin * 2, 73);
        DrawLabelValue(graphics, "CLIENTE", cotizacion.Cliente.RazonSocial, Margin + 12, boxY + 10, 300, titleFont);
        DrawLabelValue(graphics, "RUC", cotizacion.Cliente.Ruc, Margin + 12, boxY + 43, 155, titleFont);
        DrawLabelValue(graphics, "DIRECCIÓN", cotizacion.Cliente.DireccionFiscal, Margin + 178, boxY + 43, 260, titleFont);
        DrawLabelValue(graphics, "EMISIÓN", cotizacion.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy", PeruCulture),
            page.Width - 150, boxY + 10, 96, titleFont);
        DrawLabelValue(graphics, "CONDICIÓN", cotizacion.CondicionPago, page.Width - 150, boxY + 43, 96, titleFont);
    }

    private static void DrawLabelValue(
        XGraphics graphics,
        string label,
        string value,
        double x,
        double y,
        double width,
        XFont titleFont)
    {
        graphics.DrawString(label, new XFont("Arial", 6, XFontStyleEx.Bold), new XSolidBrush(Muted),
            new XRect(x, y, width, 10), XStringFormats.TopLeft);
        graphics.DrawString(value, titleFont, new XSolidBrush(Navy),
            new XRect(x, y + 11, width, 14), XStringFormats.TopLeft);
    }

    private static void DrawTableHeader(XGraphics graphics, double y, XFont font)
    {
        var x = Margin;
        var widths = new[] { 24d, 65d, 155d, 42d, 75d, 85d, 65d };
        var labels = new[] { "#", "SKU", "DESCRIPCIÓN", "CANT.", "P. UNIT.", "SUBTOTAL", "FICHA" };
        graphics.DrawRectangle(new XSolidBrush(Navy), Margin, y, widths.Sum(), TableHeaderHeight);
        for (var i = 0; i < labels.Length; i++)
        {
            graphics.DrawString(labels[i], font, XBrushes.White,
                new XRect(x + 5, y + 7, widths[i] - 10, 12), XStringFormats.TopLeft);
            x += widths[i];
        }
    }

    private static void DrawProductRow(
        PdfPage page,
        XGraphics graphics,
        double y,
        int lineNumber,
        CotizacionDetalle detalle,
        Func<int, string?> technicalSheetUrl,
        XFont bodyFont,
        XFont boldFont,
        XFont smallFont)
    {
        var widths = new[] { 24d, 65d, 155d, 42d, 75d, 85d, 65d };
        var values = new[]
        {
            lineNumber.ToString(CultureInfo.InvariantCulture),
            detalle.Producto.Sku,
            detalle.Producto.Nombre,
            detalle.Cantidad.ToString("N0", PeruCulture),
            FormatMoney(detalle.PrecioVentaCalculado),
            FormatMoney(detalle.Subtotal),
            string.IsNullOrWhiteSpace(detalle.Producto.RutaFichaTecnicaPdf) ? "No disponible" : "Abrir ficha"
        };
        graphics.DrawLine(new XPen(XColor.FromArgb(220, 225, 231)), Margin, y + ProductRowHeight,
            Margin + widths.Sum(), y + ProductRowHeight);

        var x = Margin;
        for (var i = 0; i < widths.Length; i++)
        {
            var font = i == 1 ? boldFont : bodyFont;
            graphics.DrawString(values[i], font, new XSolidBrush(Navy),
                new XRect(x + 5, y + 7, widths[i] - 10, 13), XStringFormats.TopLeft);
            if (i == widths.Length - 1 && !string.IsNullOrWhiteSpace(detalle.Producto.RutaFichaTecnicaPdf))
            {
                var fichaUrl = technicalSheetUrl(detalle.ProductoId);
                if (!string.IsNullOrWhiteSpace(fichaUrl))
                {
                    page.AddWebLink(new PdfRectangle(new XRect(x + 4, y + 4, widths[i] - 8, 20)), fichaUrl);
                }
            }

            if (i == 2)
            {
                graphics.DrawString($"U.M.: {detalle.Producto.UnidadMedida}", smallFont, new XSolidBrush(Muted),
                    new XRect(x + 5, y + 22, widths[i] - 10, 12), XStringFormats.TopLeft);
            }

            x += widths[i];
        }

    }

    private static void DrawTotals(XGraphics graphics, PdfPage page, double y, Cotizacion cotizacion, XFont bodyFont, XFont boldFont)
    {
        var x = page.Width - Margin - 205;
        graphics.DrawString("Subtotal", bodyFont, new XSolidBrush(Muted),
            new XRect(x, y, 115, 16), XStringFormats.TopLeft);
        graphics.DrawString(FormatMoney(cotizacion.Subtotal), boldFont, new XSolidBrush(Navy),
            new XRect(x + 115, y, 90, 16), XStringFormats.TopRight);
        y += 20;
        graphics.DrawString("IGV (18%)", bodyFont, new XSolidBrush(Muted),
            new XRect(x, y, 115, 16), XStringFormats.TopLeft);
        graphics.DrawString(FormatMoney(cotizacion.Igv), boldFont, new XSolidBrush(Navy),
            new XRect(x + 115, y, 90, 16), XStringFormats.TopRight);
        y += 24;
        graphics.DrawRectangle(new XSolidBrush(Navy), x, y, 205, 31);
        graphics.DrawString("TOTAL A PAGAR", boldFont, XBrushes.White,
            new XRect(x + 9, y + 9, 105, 14), XStringFormats.TopLeft);
        graphics.DrawString($"{FormatMoney(cotizacion.Total)} PEN", boldFont, XBrushes.White,
            new XRect(x + 112, y + 8, 84, 15), XStringFormats.TopRight);
        y += 43;

        var words = $"Importe en letras: {AmountInWords(cotizacion.Total)}";
        graphics.DrawString(words, new XFont("Arial", 8, XFontStyleEx.Italic), new XSolidBrush(Muted),
            new XRect(Margin, y, page.Width - Margin * 2, 32), XStringFormats.TopLeft);
        graphics.DrawString("Moneda: PEN (S/.)", bodyFont, new XSolidBrush(Muted),
            new XRect(Margin, page.Height - 44, page.Width - Margin * 2, 14), XStringFormats.TopRight);
    }

    private static string FormatMoney(decimal amount) => $"S/ {amount.ToString("N2", PeruCulture)}";

    private static string AmountInWords(decimal amount)
    {
        var absoluteAmount = Math.Abs(amount);
        var whole = decimal.ToInt64(decimal.Truncate(absoluteAmount));
        var cents = decimal.ToInt32(decimal.Round((absoluteAmount - whole) * 100m, 0, MidpointRounding.AwayFromZero));
        if (cents == 100)
        {
            whole++;
            cents = 0;
        }

        var prefix = amount < 0 ? "Menos " : string.Empty;
        return $"{prefix}{NumberInWords(whole)} soles con {cents:00}/100";
    }

    private static string NumberInWords(long number)
    {
        if (number == 0)
        {
            return "cero";
        }

        var groups = new List<string>();
        var scales = new[] { "", "mil", "millón", "mil millones", "billón", "mil billones", "trillón" };
        var groupIndex = 0;
        while (number > 0)
        {
            var group = (int)(number % 1000);
            if (group > 0)
            {
                var words = ThreeDigitNumberInWords(group);
                if (groupIndex == 1 && group == 1)
                {
                    groups.Add("mil");
                }
                else if (groupIndex > 0)
                {
                    var scale = scales[groupIndex];
                    if (group == 1 && groupIndex == 2)
                    {
                        groups.Add($"un {scale}");
                    }
                    else
                    {
                        if (groupIndex == 2 && group > 1)
                        {
                            scale = "millones";
                        }

                        groups.Add($"{ApocopateOne(words)} {scale}");
                    }
                }
                else
                {
                    groups.Add(words);
                }
            }

            number /= 1000;
            groupIndex++;
        }

        groups.Reverse();
        return string.Join(" ", groups);
    }

    private static string ThreeDigitNumberInWords(int number)
    {
        var units = new[] { "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve" };
        var tens = new[] { "", "diez", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };
        var hundreds = new[] { "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos" };

        if (number == 100)
        {
            return "cien";
        }

        var words = new List<string>();
        if (number >= 100)
        {
            words.Add(hundreds[number / 100]);
            number %= 100;
        }

        if (number is >= 10 and <= 15)
        {
            words.Add(new[] { "diez", "once", "doce", "trece", "catorce", "quince" }[number - 10]);
        }
        else if (number is >= 16 and <= 19)
        {
            words.Add($"dieci{units[number - 10]}");
        }
        else if (number is >= 21 and <= 29)
        {
            words.Add($"veinti{units[number - 20]}");
        }
        else if (number >= 20)
        {
            var remainder = number % 10;
            words.Add(remainder == 0 ? tens[number / 10] : $"{tens[number / 10]} y {units[remainder]}");
        }
        else if (number > 0)
        {
            words.Add(units[number]);
        }

        return string.Join(" ", words);
    }

    private static string ApocopateOne(string words) => words.EndsWith("veintiuno", StringComparison.Ordinal)
        ? $"{words[..^3]}ún"
        : words.EndsWith("uno", StringComparison.Ordinal)
            ? $"{words[..^3]}un"
            : words;
}

#pragma warning restore CS0618
