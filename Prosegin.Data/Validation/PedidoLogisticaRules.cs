namespace Prosegin.Data.Validation;

public static class PedidoLogisticaRules
{
    public const string MensajeSinResultados = "No se encontraron pedidos confirmados con los criterios ingresados";

    public const string EstadoTodos = "ALL";
    public const string EstadoPorPreparar = "POR_PREPARAR";
    public const string EstadoEnPreparacion = "EN_PREPARACION";
    public const string EstadoListoDespacho = "LISTO_DESPACHO";
    public const string EstadoEnRuta = "EN_RUTA";
    public const string EstadoEntregado = "ENTREGADO";

    public const string TextoPorPreparar = "Por Preparar";
    public const string TextoEnPreparacion = "Esperando al proveedor";
    public const string TextoListoDespacho = "Listo para Despacho";
    public const string TextoEnRuta = "En transporte";
    public const string TextoEntregado = "Entregado";

    public static bool EsTransicionPermitida(string? estadoActual, string? estadoNuevo)
    {
        var actual = OrdenEstado(estadoActual);
        var nuevo = OrdenEstado(estadoNuevo);
        return actual >= 0 && nuevo == actual + 1;
    }

    private static int OrdenEstado(string? estado) =>
        estado?.Trim().ToUpperInvariant() switch
        {
            "PORPREPARAR" or "POR_PREPARAR" or "ENPREPARACION" or "EN_PREPARACION" => 0,
            "LISTOPARADESPACHO" or "LISTO_DESPACHO" => 1,
            "ENRUTA" or "EN_RUTA" => 2,
            "ENTREGADO" => 3,
            _ => -1
        };

    /// <summary>
    /// Valida que el pedido cuente con venta aprobada y su respectiva orden de compra asignada.
    /// Las cotizaciones en borrador o pendientes quedan excluidas.
    /// </summary>
    public static bool EsPedidoValidoParaLogistica(bool ventaAprobada, string? numeroOrdenCompra)
    {
        return ventaAprobada && !string.IsNullOrWhiteSpace(numeroOrdenCompra);
    }

    /// <summary>
    /// Determina si la entrega está programada dentro de las próximas 24 horas respecto a la fecha de referencia.
    /// </summary>
    public static bool EsEntregaMenor24Horas(DateTime fechaEntrega, DateTime fechaReferencia)
    {
        var diferencia = fechaEntrega - fechaReferencia;
        return diferencia >= TimeSpan.Zero && diferencia <= TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Comprueba si el término de búsqueda coincide con N° de pedido, OC, RUC o Razón Social.
    /// </summary>
    public static bool CoincideBusqueda(
        string? numeroPedido,
        string? numeroOrdenCompra,
        string? ruc,
        string? razonSocial,
        string? termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
        {
            return true;
        }

        var t = QuitarTildes(termino.Trim());
        return Coincide(numeroPedido, t)
            || Coincide(numeroOrdenCompra, t)
            || Coincide(ruc, t)
            || Coincide(razonSocial, t);
    }

    private static bool Coincide(string? valor, string termino) =>
        valor != null && QuitarTildes(valor).Contains(termino, StringComparison.OrdinalIgnoreCase);

    private static string QuitarTildes(string valor)
    {
        var normalizado = valor.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalizado
            .Where(caracter => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(caracter)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray())
            .Normalize(System.Text.NormalizationForm.FormC);
    }
}
