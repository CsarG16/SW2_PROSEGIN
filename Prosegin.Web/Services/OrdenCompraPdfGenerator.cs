using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Prosegin.Data.Entities;

#pragma warning disable CS0618 // PDFsharp: conversiones de unidades en coordenadas.

namespace Prosegin.Web.Services;

public static class OrdenCompraPdfGenerator
{
    private const double Margin = 40;
    private const double PageW = 595.28; // A4 en puntos
    private const double PageH = 841.89;
    private const double ContentW = PageW - Margin * 2;
    private const double HeaderH = 92;
    private const double TableHeaderH = 24;
    private const double RowMinH = 30;
    private const double FooterReserve = 56;
    private const double TotalsBlockH = 150;

    private static readonly CultureInfo PeruCulture = CultureInfo.GetCultureInfo("es-PE");

    private static readonly XColor Navy = XColor.FromArgb(0, 13, 49);
    private static readonly XColor Orange = XColor.FromArgb(247, 167, 2);
    private static readonly XColor Ink = XColor.FromArgb(30, 41, 59);
    private static readonly XColor Muted = XColor.FromArgb(100, 116, 139);
    private static readonly XColor Line = XColor.FromArgb(226, 232, 240);
    private static readonly XColor Soft = XColor.FromArgb(247, 249, 252);
    private static readonly XColor Zebra = XColor.FromArgb(250, 251, 253);

    // Columnas de la tabla (x inicial, ancho)
    private const double ColNum = Margin + 8;
    private const double ColSku = Margin + 30;
    private const double ColDesc = Margin + 98;
    private const double ColDescW = 195;
    private const double ColUm = Margin + 300;
    private const double ColCant = Margin + 338;   // derecha: ColCant + 36
    private const double ColUnit = Margin + 380;   // derecha: ColUnit + 62
    private const double ColSub = Margin + 448;    // derecha: ColSub + 67

    private sealed record Fonts(XFont Body, XFont Bold, XFont Small, XFont SmallBold, XFont Title, XFont Big, XFont H2);

    static OrdenCompraPdfGenerator()
    {
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    public static byte[] Generate(OrdenCompra orden, string? logoPath = null)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Orden de compra {orden.Correlativo}";
        document.Info.Subject = $"Orden de compra para {orden.Proveedor.RazonSocial}";
        document.Info.Author = "Prosegin S.A.C.";

        logoPath ??= LocalizarLogo();
        var fonts = new Fonts(
            Body: new XFont("Arial", 8.5),
            Bold: new XFont("Arial", 8.5, XFontStyleEx.Bold),
            Small: new XFont("Arial", 7),
            SmallBold: new XFont("Arial", 7, XFontStyleEx.Bold),
            Title: new XFont("Arial", 20, XFontStyleEx.Bold),
            Big: new XFont("Arial", 12, XFontStyleEx.Bold),
            H2: new XFont("Arial", 9.5, XFontStyleEx.Bold));

        var detalles = orden.Detalles.OrderBy(d => d.Id).ToList();

        var page = NuevaPagina(document);
        var gfx = XGraphics.FromPdfPage(page);
        var y = DibujarCabecera(gfx, orden, fonts, logoPath);
        y = DibujarPartes(gfx, orden, fonts, y + 14);
        y = DibujarEncabezadoTabla(gfx, fonts, y + 16);

        for (var i = 0; i < detalles.Count; i++)
        {
            var detalle = detalles[i];
            var lineas = Ajustar(gfx, detalle.Producto?.Nombre ?? string.Empty, fonts.Body, ColDescW);
            var alto = Math.Max(RowMinH, lineas.Count * 11 + 14);

            if (y + alto > PageH - FooterReserve)
            {
                DibujarPie(gfx, orden, fonts, document.PageCount);
                gfx.Dispose();
                page = NuevaPagina(document);
                gfx = XGraphics.FromPdfPage(page);
                y = DibujarCabecera(gfx, orden, fonts, logoPath);
                y = DibujarEncabezadoTabla(gfx, fonts, y + 16);
            }

            DibujarFila(gfx, fonts, detalle, i + 1, lineas, y, alto, i % 2 == 1);
            y += alto;
        }

        gfx.DrawLine(new XPen(Line, 1), Margin, y, Margin + ContentW, y);

        if (y + TotalsBlockH > PageH - FooterReserve)
        {
            DibujarPie(gfx, orden, fonts, document.PageCount);
            gfx.Dispose();
            page = NuevaPagina(document);
            gfx = XGraphics.FromPdfPage(page);
            y = DibujarCabecera(gfx, orden, fonts, logoPath) - 6;
        }

        DibujarTotales(gfx, orden, fonts, y + 14);
        DibujarPie(gfx, orden, fonts, document.PageCount);
        gfx.Dispose();

        // Numeración "Página x de y" (se completa al conocer el total).
        var total = document.PageCount;
        for (var p = 0; p < total; p++)
        {
            using var g = XGraphics.FromPdfPage(document.Pages[p], XGraphicsPdfPageOptions.Append);
            g.DrawString($"Página {p + 1} de {total}", fonts.Small, new XSolidBrush(Muted),
                new XRect(Margin, PageH - 30, ContentW, 10), XStringFormats.TopRight);
        }

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    // ───────────────────────────── Estructura ─────────────────────────────

    private static PdfPage NuevaPagina(PdfDocument document)
    {
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(PageW);
        page.Height = XUnit.FromPoint(PageH);
        return page;
    }

    private static double DibujarCabecera(XGraphics gfx, OrdenCompra orden, Fonts f, string? logoPath)
    {
        // Logo
        var logoDibujado = false;
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            try
            {
                using var img = XImage.FromFile(logoPath);
                var ratio = Math.Min(190d / img.PixelWidth, 56d / img.PixelHeight);
                gfx.DrawImage(img, Margin, 26, img.PixelWidth * ratio, img.PixelHeight * ratio);
                logoDibujado = true;
            }
            catch
            {
                // Se usa el logo de texto.
            }
        }

        if (!logoDibujado)
        {
            gfx.DrawString("PROSEGIN", f.Title, new XSolidBrush(Navy), Margin, 50);
            gfx.DrawString("Prevención Integral", f.Body, new XSolidBrush(Muted), Margin, 62);
        }

        // Título + N°
        gfx.DrawString("ORDEN DE COMPRA", f.Title, new XSolidBrush(Navy),
            new XRect(Margin, 24, ContentW, 26), XStringFormats.TopRight);

        const double badgeW = 150, badgeH = 24;
        var badgeX = Margin + ContentW - badgeW;
        gfx.DrawRoundedRectangle(new XSolidBrush(Orange), badgeX, 54, badgeW, badgeH, 6, 6);
        gfx.DrawString($"N° {orden.Correlativo}", f.Big, new XSolidBrush(Navy),
            new XRect(badgeX, 54, badgeW, badgeH), XStringFormats.Center);

        // Línea de acento
        gfx.DrawRectangle(new XSolidBrush(Navy), 0, HeaderH, PageW, 3);
        gfx.DrawRectangle(new XSolidBrush(Orange), 0, HeaderH, 120, 3);
        return HeaderH + 3;
    }

    private static double DibujarPartes(XGraphics gfx, OrdenCompra orden, Fonts f, double top)
    {
        var prov = orden.Proveedor;
        var contacto = prov.Contactos.OrderByDescending(c => c.EsPrincipal).FirstOrDefault();
        const double gap = 12;
        var colW = (ContentW - gap) / 2;
        const double cardH = 118;

        // Tarjeta proveedor
        DibujarTarjeta(gfx, Margin, top, colW, cardH, "PROVEEDOR", f);
        var x = Margin + 14;
        var ty = top + 30;
        gfx.DrawString(Recortar(gfx, prov.RazonSocial, f.H2, colW - 28), f.H2, new XSolidBrush(Navy), x, ty + 8);
        ty += 20;
        ty = Dato(gfx, f, "RUC", prov.Ruc, x, ty, colW - 28);
        ty = Dato(gfx, f, "Dirección", FallBack(prov.DireccionFiscal), x, ty, colW - 28);
        var contactoTxt = contacto is null
            ? FallBack(prov.Contacto)
            : string.IsNullOrWhiteSpace(contacto.Cargo) ? contacto.Nombre : $"{contacto.Nombre} ({contacto.Cargo})";
        ty = Dato(gfx, f, "Contacto", contactoTxt, x, ty, colW - 28);
        var tel = contacto?.Telefono is { Length: > 0 } t ? t : prov.Telefono;
        var correo = contacto?.Correo is { Length: > 0 } c ? c : prov.CorreoComercial;
        Dato(gfx, f, "Tel. / Correo", $"{FallBack(tel)}  ·  {FallBack(correo)}", x, ty, colW - 28);

        // Tarjeta datos de la orden
        var x2 = Margin + colW + gap;
        DibujarTarjeta(gfx, x2, top, colW, cardH, "DATOS DE LA ORDEN", f);
        var rx = x2 + 14;
        var ry = top + 30;
        var fecha = orden.FechaCompra.Kind == DateTimeKind.Utc ? Prosegin.Web.HoraPeru.ALocal(orden.FechaCompra) : orden.FechaCompra;
        ry = Dato(gfx, f, "Fecha de emisión", fecha.ToString("dd/MM/yyyy", PeruCulture), rx, ry, colW - 28);
        ry = Dato(gfx, f, "Condición de pago", FallBack(prov.CondicionPago), rx, ry, colW - 28);
        ry = Dato(gfx, f, "Moneda", "Soles (PEN)", rx, ry, colW - 28);
        var referencia = orden.OrdenVenta?.Cotizacion?.Correlativo;
        ry = Dato(gfx, f, "Ref. pedido de venta", string.IsNullOrWhiteSpace(referencia) ? "—" : referencia, rx, ry, colW - 28);
        Dato(gfx, f, "Comprador", "Prosegin S.A.C. · Las Malvinas, Lima", rx, ry, colW - 28);

        return top + cardH;
    }

    private static void DibujarTarjeta(XGraphics gfx, double x, double y, double w, double h, string etiqueta, Fonts f)
    {
        gfx.DrawRoundedRectangle(new XPen(Line, 1), new XSolidBrush(Soft), x, y, w, h, 6, 6);
        gfx.DrawRectangle(new XSolidBrush(Orange), x, y + 10, 3, 14);
        gfx.DrawString(etiqueta, f.SmallBold, new XSolidBrush(Muted), x + 14, y + 20);
    }

    private static double Dato(XGraphics gfx, Fonts f, string etiqueta, string valor, double x, double y, double maxW)
    {
        const double labelW = 86;
        gfx.DrawString(etiqueta, f.Small, new XSolidBrush(Muted), x, y + 10);
        gfx.DrawString(Recortar(gfx, valor, f.Body, maxW - labelW), f.Body, new XSolidBrush(Ink), x + labelW, y + 10);
        return y + 14.5;
    }

    private static double DibujarEncabezadoTabla(XGraphics gfx, Fonts f, double y)
    {
        gfx.DrawRoundedRectangle(new XSolidBrush(Navy), Margin, y, ContentW, TableHeaderH, 4, 4);
        var b = XBrushes.White;
        var ty = y + 8;
        gfx.DrawString("#", f.SmallBold, b, ColNum, ty + 7);
        gfx.DrawString("CÓDIGO", f.SmallBold, b, ColSku, ty + 7);
        gfx.DrawString("DESCRIPCIÓN", f.SmallBold, b, ColDesc, ty + 7);
        gfx.DrawString("UM", f.SmallBold, b, ColUm, ty + 7);
        gfx.DrawString("CANT.", f.SmallBold, b, new XRect(ColCant, ty, 36, 10), XStringFormats.TopRight);
        gfx.DrawString("P. UNIT.", f.SmallBold, b, new XRect(ColUnit, ty, 62, 10), XStringFormats.TopRight);
        gfx.DrawString("IMPORTE", f.SmallBold, b, new XRect(ColSub, ty, 67, 10), XStringFormats.TopRight);
        return y + TableHeaderH;
    }

    private static void DibujarFila(
        XGraphics gfx, Fonts f, OrdenCompraDetalle d, int numero, List<string> lineas, double y, double alto, bool zebra)
    {
        if (zebra)
        {
            gfx.DrawRectangle(new XSolidBrush(Zebra), Margin, y, ContentW, alto);
        }

        var ink = new XSolidBrush(Ink);
        var cy = y + (alto - 10) / 2;
        gfx.DrawString(numero.ToString(), f.Body, new XSolidBrush(Muted), ColNum, cy + 8);
        gfx.DrawString(Recortar(gfx, d.Producto?.Sku ?? string.Empty, f.Body, 64), f.Body, ink, ColSku, cy + 8);

        var ty = y + (alto - lineas.Count * 11) / 2;
        foreach (var linea in lineas)
        {
            gfx.DrawString(linea, f.Body, ink, ColDesc, ty + 8);
            ty += 11;
        }

        gfx.DrawString(d.Producto?.UnidadMedida ?? "UND", f.Body, ink, ColUm, cy + 8);
        gfx.DrawString(d.Cantidad.ToString(PeruCulture), f.Body, ink, new XRect(ColCant, cy, 36, 10), XStringFormats.TopRight);
        gfx.DrawString(Dinero(d.CostoUnitario), f.Body, ink, new XRect(ColUnit, cy, 62, 10), XStringFormats.TopRight);
        var importe = d.Subtotal > 0 ? d.Subtotal : d.Cantidad * d.CostoUnitario;
        gfx.DrawString(Dinero(importe), f.Bold, ink, new XRect(ColSub, cy, 67, 10), XStringFormats.TopRight);

        gfx.DrawLine(new XPen(Line, 0.6), Margin, y + alto, Margin + ContentW, y + alto);
    }

    private static void DibujarTotales(XGraphics gfx, OrdenCompra orden, Fonts f, double y)
    {
        var total = orden.Total > 0 ? orden.Total : orden.Detalles.Sum(d => d.Subtotal > 0 ? d.Subtotal : d.Cantidad * d.CostoUnitario);

        // Importe en letras (izquierda)
        gfx.DrawString("SON:", f.SmallBold, new XSolidBrush(Muted), Margin, y + 10);
        var letras = Ajustar(gfx, MontoEnLetras(total), f.Body, 270);
        for (var i = 0; i < letras.Count && i < 3; i++)
        {
            gfx.DrawString(letras[i], f.Body, new XSolidBrush(Ink), Margin + 28, y + 10 + i * 11);
        }

        // Caja total (derecha)
        const double w = 200, h = 38;
        var x = Margin + ContentW - w;
        gfx.DrawRoundedRectangle(new XSolidBrush(Navy), x, y, w, h, 6, 6);
        gfx.DrawString("TOTAL A PAGAR", f.SmallBold, new XSolidBrush(XColor.FromArgb(200, 210, 230)), x + 14, y + 14);
        gfx.DrawString(Dinero(total, true), f.Big, new XSolidBrush(Orange),
            new XRect(x, y, w - 14, h), XStringFormats.CenterRight);

        // Condiciones
        var cy = y + 66;
        gfx.DrawRoundedRectangle(new XPen(Line, 1), new XSolidBrush(Soft), Margin, cy, ContentW, 58, 6, 6);
        gfx.DrawString("CONDICIONES Y OBSERVACIONES", f.SmallBold, new XSolidBrush(Muted), Margin + 14, cy + 15);
        var notas = new[]
        {
            $"• Condición de pago: {FallBack(orden.Proveedor.CondicionPago)}.",
            "• Indicar el número de esta orden en la guía de remisión y en la factura.",
            "• Los bienes deben coincidir con lo detallado; cualquier variación requiere autorización previa de Prosegin."
        };
        for (var i = 0; i < notas.Length; i++)
        {
            gfx.DrawString(notas[i], f.Small, new XSolidBrush(Ink), Margin + 14, cy + 27 + i * 10);
        }

        // Firmas
        var fy = cy + 58 + 44;
        if (fy + 24 < PageH - FooterReserve)
        {
            var pen = new XPen(Muted, 0.7);
            gfx.DrawLine(pen, Margin + 20, fy, Margin + 190, fy);
            gfx.DrawLine(pen, Margin + ContentW - 190, fy, Margin + ContentW - 20, fy);
            gfx.DrawString("Emitido por · Prosegin S.A.C.", f.Small, new XSolidBrush(Muted),
                new XRect(Margin + 20, fy + 4, 170, 10), XStringFormats.TopCenter);
            gfx.DrawString("Recibí conforme · Proveedor", f.Small, new XSolidBrush(Muted),
                new XRect(Margin + ContentW - 190, fy + 4, 170, 10), XStringFormats.TopCenter);
        }
    }

    private static void DibujarPie(XGraphics gfx, OrdenCompra orden, Fonts f, int _)
    {
        gfx.DrawLine(new XPen(Line, 1), Margin, PageH - 40, Margin + ContentW, PageH - 40);
        gfx.DrawString("Prosegin S.A.C. · Prevención Integral · Las Malvinas, Lima, Perú", f.Small,
            new XSolidBrush(Muted), Margin, PageH - 30);
        gfx.DrawString($"Orden de compra {orden.Correlativo}", f.SmallBold, new XSolidBrush(Navy), Margin, PageH - 20);
    }

    // ───────────────────────────── Utilidades ─────────────────────────────

    private static string? LocalizarLogo()
    {
        var candidatos = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo_horizontal.png"),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo_horizontal.png")
        };
        return candidatos.FirstOrDefault(File.Exists);
    }

    private static string FallBack(string? valor) => string.IsNullOrWhiteSpace(valor) ? "—" : valor.Trim();

    private static string Dinero(decimal valor, bool conSimbolo = false) =>
        (conSimbolo ? "S/ " : string.Empty) + valor.ToString("N2", PeruCulture);

    private static string Recortar(XGraphics gfx, string texto, XFont font, double maxAncho)
    {
        if (string.IsNullOrEmpty(texto) || gfx.MeasureString(texto, font).Width <= maxAncho)
        {
            return texto;
        }

        while (texto.Length > 1 && gfx.MeasureString(texto + "…", font).Width > maxAncho)
        {
            texto = texto[..^1];
        }

        return texto.TrimEnd() + "…";
    }

    private static List<string> Ajustar(XGraphics gfx, string texto, XFont font, double ancho)
    {
        var lineas = new List<string>();
        var actual = string.Empty;
        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidata = actual.Length == 0 ? palabra : $"{actual} {palabra}";
            if (actual.Length == 0 || gfx.MeasureString(candidata, font).Width <= ancho)
            {
                actual = candidata;
            }
            else
            {
                lineas.Add(actual);
                actual = palabra;
            }
        }

        if (actual.Length > 0)
        {
            lineas.Add(actual);
        }

        if (lineas.Count == 0)
        {
            lineas.Add(string.Empty);
        }

        return lineas;
    }

    // ─────────────────────── Importe en letras (soles) ───────────────────────

    private static string MontoEnLetras(decimal monto)
    {
        var entero = (long)Math.Floor(monto);
        var centimos = (int)Math.Round((monto - entero) * 100m, MidpointRounding.AwayFromZero);
        if (centimos == 100)
        {
            entero++;
            centimos = 0;
        }

        var letras = entero == 0 ? "cero" : NumeroEnLetras(entero);
        return $"{letras} con {centimos:00}/100 soles".ToUpper(PeruCulture);
    }

    private static readonly string[] Unidades =
    {
        "", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez", "once", "doce",
        "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve", "veinte", "veintiuno",
        "veintidós", "veintitrés", "veinticuatro", "veinticinco", "veintiséis", "veintisiete", "veintiocho", "veintinueve"
    };
    private static readonly string[] Decenas =
        { "", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };
    private static readonly string[] Centenas =
    {
        "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos",
        "ochocientos", "novecientos"
    };

    private static string Apocopar(string letras) =>
        letras.EndsWith("veintiuno", StringComparison.Ordinal) ? letras[..^9] + "veintiún"
        : letras.EndsWith("uno", StringComparison.Ordinal) ? letras[..^3] + "un"
        : letras;

    private static string NumeroEnLetras(long n)
    {
        if (n == 0) return string.Empty;
        if (n >= 1_000_000_000) return n.ToString(PeruCulture);
        if (n >= 1_000_000)
        {
            var millones = n / 1_000_000;
            var resto = n % 1_000_000;
            var cabeza = millones == 1 ? "un millón" : $"{Apocopar(NumeroEnLetras(millones))} millones";
            return resto == 0 ? cabeza : $"{cabeza} {NumeroEnLetras(resto)}";
        }

        if (n >= 1000)
        {
            var miles = n / 1000;
            var resto = n % 1000;
            var cabeza = miles == 1 ? "mil" : $"{Apocopar(NumeroEnLetras(miles))} mil";
            return resto == 0 ? cabeza : $"{cabeza} {NumeroEnLetras(resto)}";
        }

        if (n == 100) return "cien";
        if (n >= 100)
        {
            var resto = n % 100;
            return resto == 0 ? Centenas[n / 100] : $"{Centenas[n / 100]} {NumeroEnLetras(resto)}";
        }

        if (n < 30) return Unidades[n];
        var d = Decenas[n / 10];
        return n % 10 == 0 ? d : $"{d} y {Unidades[n % 10]}";
    }
}
