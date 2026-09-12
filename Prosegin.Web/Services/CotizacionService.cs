namespace Prosegin.Web.Services;

/// <summary>
/// Lógica de negocio para cotizaciones de PROSEGIN.
/// </summary>
public class CotizacionService
{
    /// <summary>
    /// Calcula el precio de venta obligatorio en base al costo del mayorista y el margen comercial:
    /// Precio = Costo / (1 - Margen)
    /// </summary>
    public decimal CalcularPrecioVenta(decimal costoProveedor, decimal margenDeseado)
    {
        if (costoProveedor < 0)
            throw new ArgumentException("El costo no puede ser negativo.", nameof(costoProveedor));

        if (margenDeseado < 0 || margenDeseado >= 1m)
            throw new ArgumentException("El margen deseado debe ser mayor o igual a 0 y menor a 1 (ej: 0.25 para 25%).", nameof(margenDeseado));

        return Math.Round(costoProveedor / (1m - margenDeseado), 2);
    }
}
