namespace Prosegin.Data.Validation;

public static class RucValidator
{
    public static bool ValidarFormato(string? ruc, out string? mensajeError)
    {
        mensajeError = null;

        if (string.IsNullOrWhiteSpace(ruc))
        {
            mensajeError = "Número de RUC inválido";
            return false;
        }

        ruc = ruc.Trim();
        if (ruc.Length != 11 || !ruc.All(caracter => caracter >= '0' && caracter <= '9')
            || (!ruc.StartsWith("10") && !ruc.StartsWith("20")))
        {
            mensajeError = "Número de RUC inválido";
            return false;
        }

        int[] factores = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            suma += (ruc[i] - '0') * factores[i];
        }

        var digitoCalculado = 11 - suma % 11;
        if (digitoCalculado == 10) digitoCalculado = 0;
        else if (digitoCalculado == 11) digitoCalculado = 1;

        if (digitoCalculado != ruc[10] - '0')
        {
            mensajeError = "Número de RUC inválido";
            return false;
        }

        return true;
    }
}
