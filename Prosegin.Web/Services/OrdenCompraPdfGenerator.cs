using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Prosegin.Data.Entities;

namespace Prosegin.Web.Services;

public static class OrdenCompraPdfGenerator
{
    private static readonly CultureInfo PeruCulture = CultureInfo.GetCultureInfo("es-PE");
    private static readonly XSolidBrush NavyBrush = new(XColor.FromArgb(11, 23, 39));
    private static readonly XSolidBrush OrangeBrush = new(XColor.FromArgb(238, 145, 28));
    private static readonly XSolidBrush LightBrush = new(XColor.FromArgb(248, 249, 251));
    private static readonly XSolidBrush MutedBrush = new(XColor.FromArgb(100, 116, 139));
    private static readonly XSolidBrush GrayBrush = new(XColor.FromArgb(128, 128, 128));
    private static readonly XSolidBrush TextBrush = new(XColor.FromArgb(32, 41, 57));
    private static readonly XPen DividerPen = new(XColor.FromArgb(230, 235, 241));

    static OrdenCompraPdfGenerator()
    {
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    public static byte[] Generate(OrdenCompra orden)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Orden de compra {orden.Correlativo}";
        document.Info.Author = "Prosegin S.A.C.";

        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var graphics = XGraphics.FromPdfPage(page);
        var regular = new XFont("Arial", 9);
        var bold = new XFont("Arial", 9, XFontStyleEx.Bold);
        var title = new XFont("Arial", 18, XFontStyleEx.Bold);

        var y = 38;
        graphics.DrawRectangle(NavyBrush, 40, y, 515, 52);
        graphics.DrawString("PROSEGIN", title, XBrushes.White, new XRect(58, y + 5, 180, 38), XStringFormats.CenterLeft);

        y += 72;
        graphics.DrawString("ORDEN DE COMPRA", title, NavyBrush,
            new XRect(40, y, 515, 30), XStringFormats.TopLeft);
        y += 34;
        graphics.DrawString(orden.Correlativo, bold, OrangeBrush, 40, y);
        graphics.DrawString($"Fecha: {orden.FechaCompra:dd/MM/yyyy}", regular, GrayBrush, 415, y);

        y += 28;
        graphics.DrawRectangle(XPens.LightGray, new XRect(40, y, 515, 58));
        graphics.DrawString("PROVEEDOR", bold, MutedBrush, 52, y + 13);
        graphics.DrawString(orden.Proveedor.RazonSocial, bold, TextBrush, 52, y + 31);
        graphics.DrawString($"RUC {orden.Proveedor.Ruc}", regular, GrayBrush, 52, y + 47);

        y += 78;
        graphics.DrawRectangle(LightBrush, 40, y, 515, 26);
        graphics.DrawString("#", bold, XBrushes.Gray, 50, y + 9);
        graphics.DrawString("Artículo", bold, GrayBrush, 85, y + 9);
        graphics.DrawString("Cant.", bold, GrayBrush, 365, y + 9);
        graphics.DrawString("Costo unitario", bold, GrayBrush, 445, y + 9);

        y += 26;
        var numero = 1;
        foreach (var detalle in orden.Detalles.OrderBy(detalle => detalle.Id))
        {
            var nombre = detalle.Producto.Nombre ?? string.Empty;
            var rectArticulo = new XRect(85, y + 8, 270, 48);
            var filasArticulo = DibujarTextoAjustado(graphics, nombre, regular, TextBrush, rectArticulo);
            var altoFila = Math.Max(34, filasArticulo * 12 + 16);

            graphics.DrawString(numero.ToString(), regular, TextBrush, 50, y + 11);
            graphics.DrawString(detalle.Cantidad.ToString(), regular, TextBrush, 365, y + 11);
            graphics.DrawString(detalle.CostoUnitario.ToString("C2", PeruCulture), regular,
                TextBrush, 445, y + 11);
            graphics.DrawLine(DividerPen, 40, y + altoFila, 555, y + altoFila);
            y += altoFila;
            numero++;
        }

        graphics.DrawString("TOTAL", bold, TextBrush, 385, y + 18);
        graphics.DrawString(orden.Total.ToString("C2", PeruCulture), bold, TextBrush, 430, y + 18);
        graphics.DrawString("Documento generado por PROSEGIN", regular, GrayBrush,
            new XRect(40, page.Height - 45, 515, 20), XStringFormats.TopCenter);

        graphics.Dispose();
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static int DibujarTextoAjustado(
        XGraphics graphics,
        string texto,
        XFont font,
        XBrush brush,
        XRect area)
    {
        var palabras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lineas = new List<string>();
        var lineaActual = string.Empty;
        var maximoAncho = area.Width;

        foreach (var palabra in palabras)
        {
            var candidata = string.IsNullOrEmpty(lineaActual) ? palabra : $"{lineaActual} {palabra}";
            if (graphics.MeasureString(candidata, font).Width <= maximoAncho || string.IsNullOrEmpty(lineaActual))
            {
                lineaActual = candidata;
            }
            else
            {
                lineas.Add(lineaActual);
                lineaActual = palabra;
            }
        }

        if (!string.IsNullOrWhiteSpace(lineaActual))
        {
            lineas.Add(lineaActual);
        }

        var lineHeight = 12;
        for (var index = 0; index < lineas.Count; index++)
        {
            graphics.DrawString(lineas[index], font, brush,
                new XRect(area.X, area.Y + index * lineHeight, area.Width, lineHeight),
                XStringFormats.TopLeft);
        }

        return lineas.Count;
    }
}
