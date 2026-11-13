namespace Prosegin.Web;

/// <summary>
/// Hora de Perú (UTC-5, sin horario de verano), independiente de la zona horaria del servidor.
/// Los hostings suelen correr en UTC; sin esto, después de las 7 pm en Lima las fechas saldrían al día siguiente.
/// </summary>
public static class HoraPeru
{
    private static readonly TimeSpan Desfase = TimeSpan.FromHours(-5);

    /// <summary>Fecha y hora actual en Lima.</summary>
    public static DateTime Ahora => DateTime.UtcNow.Add(Desfase);

    /// <summary>Fecha de hoy en Lima (hora 00:00).</summary>
    public static DateTime Hoy => Ahora.Date;

    /// <summary>Convierte una fecha UTC (o sin tipo, tratada como UTC) a hora de Lima.</summary>
    public static DateTime ALocal(DateTime fecha)
    {
        var utc = fecha.Kind == DateTimeKind.Local ? fecha.ToUniversalTime() : fecha;
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Desfase);
    }

    /// <summary>Convierte una hora de Lima a UTC para guardarla.</summary>
    public static DateTime AUtc(DateTime fechaLima) =>
        DateTime.SpecifyKind(fechaLima, DateTimeKind.Unspecified).Subtract(Desfase).ToUniversalKind();

    private static DateTime ToUniversalKind(this DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
}
