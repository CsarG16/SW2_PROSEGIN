namespace Prosegin.Data.Entities;

public class CuentaBancariaProveedor
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public string Banco { get; set; } = string.Empty;
    public string Moneda { get; set; } = "PEN";
    public string NumeroCuenta { get; set; } = string.Empty;
    public string Cci { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; }

    public Proveedor Proveedor { get; set; } = null!;
}
