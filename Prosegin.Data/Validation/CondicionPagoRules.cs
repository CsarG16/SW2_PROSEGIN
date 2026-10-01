namespace Prosegin.Data.Validation;

public static class CondicionPagoRules
{
    public const string PlazoCreditoPredeterminado = "7 días";

    public static bool EsPlazoCreditoValido(string? condicionPago)
    {
        return string.Equals(condicionPago?.Trim(), "7 días", StringComparison.OrdinalIgnoreCase)
            || string.Equals(condicionPago?.Trim(), "15 días", StringComparison.OrdinalIgnoreCase)
            || string.Equals(condicionPago?.Trim(), "30 días", StringComparison.OrdinalIgnoreCase);
    }
}
