namespace Prosegin.Data.Validation;

public static class PedidoLogisticaRules
{
    public const string MensajeSinResultados = "No se encontraron pedidos confirmados con los criterios ingresados";

    public const string EstadoTodos = "ALL";
    public const string EstadoPorPreparar = "POR_PREPARAR";
    public const string EstadoEnPreparacion = "EN_PREPARACION";
    public const string EstadoListoDespacho = "LISTO_DESPACHO";

    public const string TextoPorPreparar = "Por Preparar";
    public const string TextoEnPreparacion = "En Preparación";
    public const string TextoListoDespacho = "Listo para Despacho";

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
        // Si la entrega es hoy (o en el transcurso del día) o dentro de 24 horas calendario
        return (diferencia.TotalHours >= -12 && diferencia.TotalHours <= 24)
            || (fechaEntrega.Date == fechaReferencia.Date);
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

        var t = termino.Trim();
        return (numeroPedido != null && numeroPedido.Contains(t, StringComparison.OrdinalIgnoreCase))
            || (numeroOrdenCompra != null && numeroOrdenCompra.Contains(t, StringComparison.OrdinalIgnoreCase))
            || (ruc != null && ruc.Contains(t, StringComparison.OrdinalIgnoreCase))
            || (razonSocial != null && razonSocial.Contains(t, StringComparison.OrdinalIgnoreCase));
    }
}
