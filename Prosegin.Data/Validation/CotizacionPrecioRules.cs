namespace Prosegin.Data.Validation;

/// <summary>
/// Reglas de negocio para HU 3.2: Ajuste de precios, descuentos y márgenes en cotizaciones.
/// </summary>
public static class CotizacionPrecioRules
{
    public const string MensajePrecioMenorACosto = "El precio cotizado no puede ser menor al costo base registrado";
    public const string MensajeGuardadoExitoso = "Productos guardados correctamente";

    /// <summary>
    /// El sistema impide que cualquier ítem se cotice por debajo de su costo base para evitar márgenes negativos.
    /// </summary>
    public static bool EsPrecioValido(decimal precioUnitario, decimal costoBase)
    {
        return precioUnitario >= costoBase;
    }

    /// <summary>
    /// Recalcula el subtotal del ítem: Cantidad * Precio Unitario.
    /// </summary>
    public static decimal CalcularSubtotalItem(int cantidad, decimal precioUnitario)
    {
        if (cantidad <= 0) return 0m;
        return Math.Round(cantidad * precioUnitario, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Margen porcentual del ítem respecto al costo base: ((Precio - Costo) / Costo) * 100.
    /// </summary>
    public static decimal CalcularMargenItem(decimal precioUnitario, decimal costoBase)
    {
        if (costoBase <= 0) return 0m;
        return Math.Round(((precioUnitario - costoBase) / costoBase) * 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Cálculo de IGV del 18% sobre el subtotal.
    /// </summary>
    public static decimal CalcularIgv(decimal subtotal)
    {
        if (subtotal <= 0) return 0m;
        return Math.Round(subtotal * 0.18m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Total estimado final: Subtotal + IGV.
    /// </summary>
    public static decimal CalcularTotal(decimal subtotal, decimal igv)
    {
        return Math.Round(subtotal + igv, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Ganancia total en soles: Subtotal - Costo Total.
    /// </summary>
    public static decimal CalcularGanancia(decimal subtotal, decimal costoTotal)
    {
        return Math.Round(subtotal - costoTotal, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Margen global en porcentaje: ((Subtotal - CostoTotal) / CostoTotal) * 100.
    /// </summary>
    public static decimal CalcularMargenGlobal(decimal subtotal, decimal costoTotal)
    {
        if (costoTotal <= 0) return 0m;
        return Math.Round(((subtotal - costoTotal) / costoTotal) * 100m, 2, MidpointRounding.AwayFromZero);
    }
}
