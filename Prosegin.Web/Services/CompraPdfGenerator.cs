using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Prosegin.Data.Entities;

#pragma warning disable CS0618 // PDFsharp: Unit conversions in points

namespace Prosegin.Web.Services;

/// <summary>
/// Genera localmente el comprobante PDF oficial de Prosegin para las compras formalizadas
/// (Factura Electrónica / Constancia de Compra) respetando los importes, tasa IGV y condiciones de emisión.
/// </summary>
public static class CompraPdfGenerator
{
    private const double Margin = 40;
    private const double HeaderH = 92;
    private const double TableHeaderH = 26;
    private const double RowH = 34;
    private const double FooterH = 44;
    private const double TotalsBlockH = 152;

    private static readonly CultureInfo PeruCulture = CultureInfo.GetCultureInfo("es-PE");
    private static readonly XColor Navy = XColor.FromArgb(0, 13, 49);
    private static readonly XColor Orange = XColor.FromArgb(247, 167, 2);
    private static readonly XColor LightBg = XColor.FromArgb(247, 248, 250);
    private static readonly XColor Divider = XColor.FromArgb(226, 232, 240);
    private static readonly XColor Muted = XColor.FromArgb(100, 116, 139);
    private static readonly XColor OrangeLight = XColor.FromArgb(255, 248, 225);

    // Total = 515 pts (A4 595.28 - 80 margins)
    private static readonly double[] ColW = { 22, 74, 212, 34, 42, 63, 68 };

    static CompraPdfGenerator()
    {
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    private sealed record FontBundle(
        XFont Body,
        XFont Bold8,
        XFont Bold9,
        XFont Bold10,
        XFont Bold12,
        XFont Small,
        XFont SmallBold,
        XFont Italic8);

    private sealed record PdfLine(
        string Sku,
        string Descripcion,
        string UnidadMedida,
        decimal Cantidad,
        decimal ValorUnitario,
        decimal Subtotal);

    public static byte[] Generate(OrdenVenta venta, Factura? factura, string? logoPath = null)
    {
        using var document = new PdfDocument();
        var numeroDoc = !string.IsNullOrWhiteSpace(factura?.NumeroFactura)
            ? factura.NumeroFactura
            : !string.IsNullOrWhiteSpace(venta.Cotizacion.NumeroOrdenCompraCliente)
                ? venta.Cotizacion.NumeroOrdenCompraCliente
                : $"OV-{venta.Id:D6}";

        var esFactura = factura != null;
        var tituloDoc = esFactura ? "FACTURA ELECTRÓNICA" : "CONSTANCIA DE COMPRA";
        document.Info.Title = $"{tituloDoc} {numeroDoc}";
        document.Info.Author = "Prosegin Prevención Integral S.A.C.";
        document.Info.Subject = $"Comprobante comercial para {venta.Cotizacion.Cliente.RazonSocial}";

        var f = new FontBundle(
            Body: new XFont("Arial", 8, XFontStyleEx.Regular),
            Bold8: new XFont("Arial", 8, XFontStyleEx.Bold),
            Bold9: new XFont("Arial", 9, XFontStyleEx.Bold),
            Bold10: new XFont("Arial", 10, XFontStyleEx.Bold),
            Bold12: new XFont("Arial", 11.5, XFontStyleEx.Bold),
            Small: new XFont("Arial", 7, XFontStyleEx.Regular),
            SmallBold: new XFont("Arial", 7, XFontStyleEx.Bold),
            Italic8: new XFont("Arial", 7.8, XFontStyleEx.Italic));

        var items = ObtenerLineas(venta, factura);
        var subtotal = factura != null && factura.Detalles.Count > 0
            ? factura.Detalles.Sum(d => d.Subtotal)
            : venta.Cotizacion.Subtotal;
        var igv = factura != null && factura.Detalles.Count > 0
            ? factura.Detalles.Sum(d => d.Igv)
            : venta.Cotizacion.Igv;
        var total = factura?.Total ?? venta.Cotizacion.Total;
        var fechaEmision = factura?.FechaEmision ?? venta.FechaCreacion;
        var fechaVencimiento = factura?.FechaVencimiento;

        var page = NewPage(document);
        var gfx = XGraphics.FromPdfPage(page);

        var y = DrawHeader(gfx, page, tituloDoc, numeroDoc, f, logoPath);
        y += 12;
        y = DrawFiscalBlock(gfx, page, venta, fechaEmision, fechaVencimiento, f, y);
        y += 14;
        DrawTableHeader(gfx, y, f.Bold8);
        y += TableHeaderH;

        var maxTableY = page.Height - Margin - FooterH - 10;
        for (var i = 0; i < items.Count; i++)
        {
            if (y + RowH > maxTableY)
            {
                DrawFooter(gfx, page, f, document.PageCount);
                gfx.Dispose();
                page = NewPage(document);
                gfx = XGraphics.FromPdfPage(page);
                y = DrawHeader(gfx, page, tituloDoc, numeroDoc, f, logoPath) + 14;
                DrawTableHeader(gfx, y, f.Bold8);
                y += TableHeaderH;
            }

            DrawItemRow(gfx, y, i + 1, items[i], f);
            y += RowH;
        }

        if (y + TotalsBlockH + FooterH > page.Height - Margin)
        {
            DrawFooter(gfx, page, f, document.PageCount);
            gfx.Dispose();
            page = NewPage(document);
            gfx = XGraphics.FromPdfPage(page);
            y = DrawHeader(gfx, page, tituloDoc, numeroDoc, f, logoPath) + 18;
        }
        else
        {
            y += 14;
        }

        DrawTotalsAndNote(gfx, page, y, numeroDoc, esFactura, factura?.CodigoHash, subtotal, igv, total, venta.Cotizacion.CondicionPago, f);
        DrawFooter(gfx, page, f, document.PageCount);
        gfx.Dispose();

        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }

    private static List<PdfLine> ObtenerLineas(OrdenVenta venta, Factura? factura)
    {
        if (factura != null && factura.Detalles.Count > 0)
        {
            return factura.Detalles
                .OrderBy(d => d.Id)
                .Select(d => new PdfLine(
                    d.Codigo,
                    d.Descripcion,
                    d.UnidadMedida,
                    d.Cantidad,
                    d.ValorUnitario,
                    d.Subtotal))
                .ToList();
        }

        return venta.Cotizacion.Detalles
            .OrderBy(d => d.Id)
            .Select(d => new PdfLine(
                d.Producto?.Sku ?? "—",
                string.IsNullOrWhiteSpace(d.NombreProducto) ? (d.Producto?.Nombre ?? string.Empty) : d.NombreProducto,
                d.Producto?.UnidadMedida ?? "UND",
                d.Cantidad,
                d.PrecioVentaCalculado,
                d.Subtotal))
            .ToList();
    }

    private static PdfPage NewPage(PdfDocument document)
    {
        var page = document.AddPage();
        page.Width = XUnit.FromMillimeter(210);
        page.Height = XUnit.FromMillimeter(297);
        return page;
    }

    private static double DrawHeader(
        XGraphics gfx,
        PdfPage page,
        string tituloDoc,
        string numeroDoc,
        FontBundle f,
        string? logoPath)
    {
        const double topY = 28;
        double logoOffsetY = topY;

        // Acento superior azul marino + línea naranja
        gfx.DrawRectangle(new XSolidBrush(Navy), 0, 0, page.Width, 8);
        gfx.DrawRectangle(new XSolidBrush(Orange), 0, 8, page.Width, 3);

        // Emisor izquierda
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            try
            {
                using var img = XImage.FromFile(logoPath);
                double maxW = 135, maxH = 32;
                var ratio = Math.Min(maxW / img.PixelWidth, maxH / img.PixelHeight);
                var lh = img.PixelHeight * ratio;
                gfx.DrawImage(img, Margin, topY, img.PixelWidth * ratio, lh);
                logoOffsetY = topY + lh + 6;
            }
            catch
            {
                gfx.DrawString("PROSEGIN", f.Bold12, new XSolidBrush(Navy),
                    new XRect(Margin, topY, 200, 18), XStringFormats.TopLeft);
                logoOffsetY = topY + 22;
            }
        }
        else
        {
            gfx.DrawString("PROSEGIN", f.Bold12, new XSolidBrush(Navy),
                new XRect(Margin, topY, 200, 18), XStringFormats.TopLeft);
            logoOffsetY = topY + 22;
        }

        gfx.DrawString("PROSEGIN PREVENCIÓN INTEGRAL S.A.C.", f.Bold9, new XSolidBrush(Navy),
            new XRect(Margin, logoOffsetY, 290, 12), XStringFormats.TopLeft);
        gfx.DrawString("Distribución y Gestión Técnica de Equipos de Protección Individual (EPP)", f.Small, new XSolidBrush(Muted),
            new XRect(Margin, logoOffsetY + 13, 300, 10), XStringFormats.TopLeft);
        gfx.DrawString("Av. Los Industriales 450, Urb. Industrial Vulcano, Ate - Lima", f.Small, new XSolidBrush(Muted),
            new XRect(Margin, logoOffsetY + 24, 300, 10), XStringFormats.TopLeft);
        gfx.DrawString("Central: (01) 640-9200  |  contacto@prosegin.pe", f.Small, new XSolidBrush(Muted),
            new XRect(Margin, logoOffsetY + 35, 300, 10), XStringFormats.TopLeft);

        // Recuadro fiscal derecho estilo comprobante SUNAT
        double boxW = 190, boxH = 78;
        var boxX = page.Width - Margin - boxW;
        double boxY = topY;

        gfx.DrawRoundedRectangle(new XPen(Navy, 1.5), XBrushes.White, boxX, boxY, boxW, boxH, 5, 5);
        gfx.DrawString("R.U.C. 20601234567", f.Bold10, new XSolidBrush(Navy),
            new XRect(boxX, boxY + 5, boxW, 18), XStringFormats.Center);

        double stripY = boxY + 25;
        double stripH = 24;
        gfx.DrawRectangle(new XSolidBrush(Navy), boxX, stripY, boxW, stripH);
        gfx.DrawString(tituloDoc, f.Bold10, XBrushes.White,
            new XRect(boxX, stripY + 4, boxW, stripH - 4), XStringFormats.Center);

        gfx.DrawString(numeroDoc, f.Bold12, new XSolidBrush(Navy),
            new XRect(boxX, stripY + stripH + 6, boxW, 20), XStringFormats.Center);

        return Math.Max(topY + HeaderH, boxY + boxH + 8);
    }

    private static double DrawFiscalBlock(
        XGraphics gfx,
        PdfPage page,
        OrdenVenta venta,
        DateTime fechaEmision,
        DateTime? fechaVencimiento,
        FontBundle f,
        double top)
    {
        var cot = venta.Cotizacion;
        var cliente = cot.Cliente;
        var w = page.Width - Margin * 2;
        const double blockH = 114;

        gfx.DrawRoundedRectangle(new XSolidBrush(LightBg), Margin, top, w, blockH, 4, 4);
        gfx.DrawRoundedRectangle(new XSolidBrush(Orange), Margin, top, 4, blockH, 2, 2);

        var lx = Margin + 14;
        var colRightX = page.Width - Margin - 210;

        // Fila 1
        DrawPair(gfx, "CLIENTE / SEÑOR(ES)", cliente.RazonSocial, lx, top + 10, w * 0.54, f);
        DrawPair(gfx, "FECHA DE EMISIÓN", fechaEmision.ToLocalTime().ToString("dd/MM/yyyy", PeruCulture), colRightX, top + 10, 96, f);
        DrawPair(gfx, "FECHA VENCIMIENTO",
            fechaVencimiento.HasValue
                ? fechaVencimiento.Value.ToLocalTime().ToString("dd/MM/yyyy", PeruCulture)
                : "—",
            colRightX + 104, top + 10, 100, f);

        // Fila 2
        var row2Y = top + 44;
        gfx.DrawLine(new XPen(Divider), lx, row2Y - 4, page.Width - Margin - 12, row2Y - 4);
        DrawPair(gfx, "R.U.C.", cliente.Ruc, lx, row2Y, 80, f);
        DrawPair(gfx, "DIRECCIÓN FISCAL", cliente.DireccionFiscal, lx + 88, row2Y, w * 0.39, f);
        DrawPair(gfx, "FORMA DE PAGO", cot.CondicionPago, colRightX, row2Y, 200, f);

        // Fila 3
        var row3Y = top + 78;
        gfx.DrawLine(new XPen(Divider), lx, row3Y - 4, page.Width - Margin - 12, row3Y - 4);
        DrawPair(gfx, "MONEDA", "Soles (PEN - S/)", lx, row3Y, 110, f);
        DrawPair(gfx, "N.° COTIZACIÓN / PEDIDO", cot.Correlativo, lx + 120, row3Y, 145, f);
        DrawPair(gfx, "ORDEN DE COMPRA CLIENTE",
            string.IsNullOrWhiteSpace(cot.NumeroOrdenCompraCliente) ? "—" : cot.NumeroOrdenCompraCliente,
            colRightX, row3Y, 200, f);

        return top + blockH;
    }

    private static void DrawPair(XGraphics gfx, string label, string value, double x, double y, double maxW, FontBundle f)
    {
        gfx.DrawString(label, f.Small, new XSolidBrush(Muted), new XRect(x, y, maxW, 10), XStringFormats.TopLeft);
        gfx.DrawString(Truncate(value, maxW, f.Bold8, gfx), f.Bold8, new XSolidBrush(Navy),
            new XRect(x, y + 10, maxW, 14), XStringFormats.TopLeft);
    }

    private static void DrawTableHeader(XGraphics gfx, double y, XFont font)
    {
        var tableW = ColW.Sum();
        var labels = new[] { "#", "CÓDIGO", "DESCRIPCIÓN", "U.M.", "CANT.", "V. UNIT.", "SUBTOTAL" };
        gfx.DrawRectangle(new XSolidBrush(Navy), Margin, y, tableW, TableHeaderH);
        gfx.DrawRectangle(new XSolidBrush(Orange), Margin, y, 4, TableHeaderH);

        double x = Margin + 4;
        for (var i = 0; i < labels.Length; i++)
        {
            var format = i switch
            {
                0 or 3 => XStringFormats.Center,
                4 or 5 or 6 => XStringFormats.TopRight,
                _ => XStringFormats.TopLeft
            };

            gfx.DrawString(labels[i], font, XBrushes.White,
                new XRect(x + 4, y + 7, ColW[i] - 8, 14), format);
            x += ColW[i];
        }
    }

    private static void DrawItemRow(XGraphics gfx, double y, int num, PdfLine line, FontBundle f)
    {
        var tableW = ColW.Sum();
        if (num % 2 == 0)
        {
            gfx.DrawRectangle(new XSolidBrush(LightBg), Margin, y, tableW, RowH);
        }

        gfx.DrawLine(new XPen(Divider), Margin, y + RowH, Margin + tableW, y + RowH);

        double x = Margin + 4;
        for (var i = 0; i < ColW.Length; i++)
        {
            var cx = x + 4;
            var cw = ColW[i] - 8;
            if (i == 0)
            {
                double size = 16;
                var circleX = cx + (cw - size) / 2;
                var circleY = y + (RowH - size) / 2;
                gfx.DrawEllipse(new XSolidBrush(OrangeLight), circleX, circleY, size, size);
                gfx.DrawString(num.ToString(CultureInfo.InvariantCulture), f.Small, new XSolidBrush(Orange),
                    new XRect(circleX, circleY + 2, size, 14), XStringFormats.Center);
            }
            else if (i == 1)
            {
                gfx.DrawString(Truncate(line.Sku, cw, f.Bold8, gfx), f.Bold8, new XSolidBrush(Navy),
                    new XRect(cx, y + 10, cw, 14), XStringFormats.TopLeft);
            }
            else if (i == 2)
            {
                gfx.DrawString(Truncate(line.Descripcion, cw, f.Body, gfx), f.Body, new XSolidBrush(Navy),
                    new XRect(cx, y + 10, cw, 14), XStringFormats.TopLeft);
            }
            else if (i == 3)
            {
                gfx.DrawString(line.UnidadMedida, f.Small, new XSolidBrush(Muted),
                    new XRect(cx, y + 11, cw, 14), XStringFormats.Center);
            }
            else if (i == 4)
            {
                var cantStr = line.Cantidad == decimal.Truncate(line.Cantidad)
                    ? line.Cantidad.ToString("N0", PeruCulture)
                    : line.Cantidad.ToString("N2", PeruCulture);
                gfx.DrawString(cantStr, f.Body, new XSolidBrush(Navy),
                    new XRect(cx, y + 10, cw, 14), XStringFormats.TopRight);
            }
            else if (i == 5)
            {
                gfx.DrawString(CotizacionPdfGenerator.FormatMoney(line.ValorUnitario), f.Body, new XSolidBrush(Navy),
                    new XRect(cx, y + 10, cw, 14), XStringFormats.TopRight);
            }
            else if (i == 6)
            {
                gfx.DrawString(CotizacionPdfGenerator.FormatMoney(line.Subtotal), f.Bold8, new XSolidBrush(Navy),
                    new XRect(cx, y + 10, cw, 14), XStringFormats.TopRight);
            }

            x += ColW[i];
        }
    }

    private static void DrawTotalsAndNote(
        XGraphics gfx,
        PdfPage page,
        double y,
        string numeroDoc,
        bool esFactura,
        string? codigoHash,
        decimal subtotal,
        decimal igv,
        decimal total,
        string condicionPago,
        FontBundle f)
    {
        var igvPct = subtotal > 0m
            ? decimal.Round((igv / subtotal) * 100m, 0, MidpointRounding.AwayFromZero)
            : 18m;

        double leftW = 285;
        gfx.DrawRoundedRectangle(new XSolidBrush(LightBg), Margin, y, leftW, 50, 4, 4);
        gfx.DrawString("IMPORTE EN LETRAS", f.SmallBold, new XSolidBrush(Muted),
            new XRect(Margin + 10, y + 7, leftW - 20, 10), XStringFormats.TopLeft);
        gfx.DrawString($"SON: {CotizacionPdfGenerator.AmountInWords(total).ToUpper(PeruCulture)}",
            f.Bold8, new XSolidBrush(Navy),
            new XRect(Margin + 10, y + 20, leftW - 20, 24), XStringFormats.TopLeft);

        double infoY = y + 58;
        gfx.DrawRoundedRectangle(new XSolidBrush(LightBg), Margin, infoY, leftW, 64, 4, 4);
        gfx.DrawString($"RUC Emisor: 20601234567  |  Doc.: {numeroDoc}", f.SmallBold, new XSolidBrush(Navy),
            new XRect(Margin + 10, infoY + 8, leftW - 20, 12), XStringFormats.TopLeft);
        gfx.DrawString($"Forma de pago: {condicionPago}  |  Moneda: PEN (Soles)", f.Small, new XSolidBrush(Muted),
            new XRect(Margin + 10, infoY + 22, leftW - 20, 12), XStringFormats.TopLeft);
        var pieFiscal = esFactura && !string.IsNullOrWhiteSpace(codigoHash)
            ? $"HASH: {codigoHash}"
            : "Representación impresa generada por PROSEGIN con condiciones vigentes al emitir.";
        gfx.DrawString(Truncate(pieFiscal, leftW - 20, f.Italic8, gfx), f.Italic8, new XSolidBrush(Muted),
            new XRect(Margin + 10, infoY + 37, leftW - 20, 16), XStringFormats.TopLeft);

        double panelW = 212;
        var panelX = page.Width - Margin - panelW;
        DrawTotalLine(gfx, "Op. Gravada / Subtotal", CotizacionPdfGenerator.FormatMoney(subtotal), panelX, y, panelW, f, highlight: false);
        DrawTotalLine(gfx, "Op. Exonerada", CotizacionPdfGenerator.FormatMoney(0m), panelX, y + 21, panelW, f, highlight: false);
        DrawTotalLine(gfx, "Op. Inafecta", CotizacionPdfGenerator.FormatMoney(0m), panelX, y + 42, panelW, f, highlight: false);
        DrawTotalLine(gfx, $"I.G.V. ({igvPct}%)", CotizacionPdfGenerator.FormatMoney(igv), panelX, y + 63, panelW, f, highlight: false);
        DrawTotalLine(gfx, "IMPORTE TOTAL", CotizacionPdfGenerator.FormatMoney(total), panelX, y + 88, panelW, f, highlight: true);
    }

    private static void DrawTotalLine(
        XGraphics gfx,
        string label,
        string amount,
        double x,
        double y,
        double w,
        FontBundle f,
        bool highlight)
    {
        if (highlight)
        {
            gfx.DrawRoundedRectangle(new XSolidBrush(Navy), x, y, w, 30, 4, 4);
            gfx.DrawString(label, f.Bold9, XBrushes.White, new XRect(x + 10, y + 8, 110, 16), XStringFormats.TopLeft);
            gfx.DrawString(amount, f.Bold10, new XSolidBrush(Orange), new XRect(x + 90, y + 7, w - 100, 16), XStringFormats.TopRight);
            return;
        }

        gfx.DrawLine(new XPen(Divider), x, y + 17, x + w, y + 17);
        gfx.DrawString(label, f.Body, new XSolidBrush(Muted), new XRect(x + 4, y + 2, 120, 14), XStringFormats.TopLeft);
        gfx.DrawString(amount, f.Bold8, new XSolidBrush(Navy), new XRect(x + 95, y + 2, w - 100, 14), XStringFormats.TopRight);
    }

    private static void DrawFooter(XGraphics gfx, PdfPage page, FontBundle f, int pageNum)
    {
        var y = page.Height - Margin - 22;
        gfx.DrawLine(new XPen(Divider), Margin, y, page.Width - Margin, y);
        gfx.DrawString("PROSEGIN PREVENCIÓN INTEGRAL S.A.C. — R.U.C. 20601234567 — Comprobante comercial oficial",
            f.Small, new XSolidBrush(Muted), new XRect(Margin, y + 6, 400, 12), XStringFormats.TopLeft);
        gfx.DrawString($"Página {pageNum}", f.Small, new XSolidBrush(Muted),
            new XRect(page.Width - Margin - 60, y + 6, 60, 12), XStringFormats.TopRight);
    }

    private static string Truncate(string text, double maxW, XFont font, XGraphics gfx)
    {
        if (string.IsNullOrEmpty(text) || gfx.MeasureString(text, font).Width <= maxW)
            return text;

        const string ellipsis = "…";
        int low = 1, high = text.Length, best = 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (gfx.MeasureString(text[..mid] + ellipsis, font).Width <= maxW)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return text[..best] + ellipsis;
    }
}

#pragma warning restore CS0618
