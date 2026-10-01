namespace Prosegin.Data.Validation;

public static class ClienteBusquedaRules
{
    public const string MensajeSinResultados = "No se encontraron clientes registrados con los datos ingresados";

    public static bool EsEntradaSoloNumeros(string? termino)
    {
        return !string.IsNullOrWhiteSpace(termino)
            && termino.All(caracter => caracter >= '0' && caracter <= '9');
    }

    public static bool PuedeAbrirCotizacion(string? estadoSunat, string? condicionSunat)
    {
        return string.Equals(estadoSunat, "ACTIVO", StringComparison.OrdinalIgnoreCase)
            && string.Equals(condicionSunat, "HABIDO", StringComparison.OrdinalIgnoreCase);
    }
}
