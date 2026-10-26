using Microsoft.EntityFrameworkCore;
using Prosegin.Data.Entities;

namespace Prosegin.Data;

public class ProseginDbContext : DbContext
{
    public ProseginDbContext(DbContextOptions<ProseginDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Producto> Productos { get; set; }
    public DbSet<PuntoEntrega> PuntosEntrega { get; set; }
    public DbSet<ContactoCliente> ContactosCliente { get; set; }
    public DbSet<Cotizacion> Cotizaciones { get; set; }
    public DbSet<CotizacionDetalle> CotizacionDetalles { get; set; }
    public DbSet<OrdenVenta> OrdenesVenta { get; set; }
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<ContactoProveedor> ContactosProveedor { get; set; }
    public DbSet<CuentaBancariaProveedor> CuentasBancariasProveedor { get; set; }
    public DbSet<ProductoProveedor> ProductoProveedores { get; set; }
    public DbSet<OrdenCompra> OrdenesCompra { get; set; }
    public DbSet<OrdenCompraDetalle> OrdenCompraDetalles { get; set; }
    public DbSet<Factura> Facturas { get; set; }
    public DbSet<FacturaDetalle> FacturaDetalles { get; set; }
    public DbSet<GuiaRemision> GuiasRemision { get; set; }
    public DbSet<GuiaRemisionDetalle> GuiaRemisionDetalles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de la tabla Clientes
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Clientes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ruc).HasMaxLength(11).IsRequired();
            entity.Property(e => e.RazonSocial).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DireccionFiscal).HasMaxLength(250);
            entity.Property(e => e.Referencia).HasMaxLength(250);
            entity.Property(e => e.Telefono).HasMaxLength(20);
            entity.Property(e => e.CorreoElectronico).HasMaxLength(100);
        });

        // Configuración de la tabla Productos (EPP)
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sku).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Sku).IsUnique();
            entity.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Categoria).HasMaxLength(100);
            entity.Property(e => e.UnidadMedida).HasMaxLength(20);
            entity.Property(e => e.PesoKg).HasPrecision(18, 3);
            entity.Property(e => e.CostoReferencial).HasPrecision(18, 2);
            entity.Property(e => e.PesoKg).HasPrecision(18, 2).HasDefaultValue(0.50m);
            entity.Property(e => e.StockDisponible).HasDefaultValue(0);
            entity.Property(e => e.RutaFichaTecnicaPdf).HasMaxLength(300);
            entity.Property(e => e.NombreArchivoPdf).HasMaxLength(150);
        });

        modelBuilder.Entity<Cotizacion>(entity =>
        {
            entity.ToTable("Cotizaciones");
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.Igv).HasPrecision(18, 2);
            entity.Property(e => e.Total).HasPrecision(18, 2);
            entity.Property(e => e.NumeroOrdenCompraCliente).HasMaxLength(100);
            entity.Property(e => e.RutaOrdenCompraCliente).HasMaxLength(40);
            entity.Property(e => e.NombreArchivoOrdenCompraCliente).HasMaxLength(255);
        });

        modelBuilder.Entity<CotizacionDetalle>(entity =>
        {
            entity.ToTable("CotizacionDetalles");
            entity.Property(e => e.NombreProducto).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CostoProveedorReferencial).HasPrecision(18, 2);
            entity.Property(e => e.MargenDeseado).HasPrecision(18, 2);
            entity.Property(e => e.PrecioVentaCalculado).HasPrecision(18, 2);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OrdenCompra>(entity =>
        {
            entity.ToTable("OrdenesCompra");
            entity.Property(e => e.Total).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.ToTable("Proveedores");
            entity.HasIndex(e => e.Ruc).IsUnique();
            entity.Property(e => e.Ruc).HasMaxLength(11).IsRequired();
            entity.Property(e => e.DireccionFiscal).HasMaxLength(250).HasDefaultValue(string.Empty);
            entity.Property(e => e.CorreoComercial).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.NombreComercial).HasMaxLength(150).HasDefaultValue(string.Empty);
            entity.Property(e => e.Especialidad).HasMaxLength(200).HasDefaultValue(string.Empty);
            entity.Property(e => e.CondicionPago).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.EstadoSunat).HasMaxLength(30).HasDefaultValue("NO VERIFICADO");
            entity.Property(e => e.CondicionSunat).HasMaxLength(30).HasDefaultValue("NO VERIFICADO");
            entity.Property(e => e.Activo).HasDefaultValue(true);
        });

        modelBuilder.Entity<ContactoProveedor>(entity =>
        {
            entity.ToTable("ContactosProveedor");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Cargo).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Telefono).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Correo).HasMaxLength(150).IsRequired();
            entity.HasOne(e => e.Proveedor)
                .WithMany(e => e.Contactos)
                .HasForeignKey(e => e.ProveedorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CuentaBancariaProveedor>(entity =>
        {
            entity.ToTable("CuentasBancariasProveedor");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Banco).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Moneda).HasMaxLength(3).HasDefaultValue("PEN").IsRequired();
            entity.Property(e => e.NumeroCuenta).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Cci).HasMaxLength(20).IsRequired();
            entity.Property(e => e.EsPrincipal).HasDefaultValue(false);
            entity.HasOne(e => e.Proveedor)
                .WithMany(e => e.CuentasBancarias)
                .HasForeignKey(e => e.ProveedorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductoProveedor>(entity =>
        {
            entity.ToTable("ProductoProveedores");
            entity.HasKey(e => new { e.ProductoId, e.ProveedorId });
            entity.Property(e => e.CostoCompra).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.EsPrincipal).HasDefaultValue(false);
            entity.Property(e => e.PlazoEntregaHoras).HasDefaultValue(24);
            entity.HasOne(e => e.Producto)
                .WithMany(e => e.ProveedoresAutorizados)
                .HasForeignKey(e => e.ProductoId);
            entity.HasOne(e => e.Proveedor)
                .WithMany(e => e.ProductosAutorizados)
                .HasForeignKey(e => e.ProveedorId);
        });

        modelBuilder.Entity<OrdenCompraDetalle>(entity =>
        {
            entity.ToTable("OrdenCompraDetalles");
            entity.Property(e => e.CostoUnitario).HasPrecision(18, 2);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Factura>(entity =>
        {
            entity.ToTable("Facturas");
            entity.Property(e => e.Total).HasPrecision(18, 2);
            entity.Property(e => e.EstadoEmision).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Serie).HasMaxLength(10);
            entity.Property(e => e.CodigoHash).HasMaxLength(255);
            entity.Property(e => e.CadenaQr).HasMaxLength(1000);
            entity.Property(e => e.EnlacePdf).HasMaxLength(500);
            entity.Property(e => e.EnlaceXml).HasMaxLength(500);
            entity.Property(e => e.EnlaceCdr).HasMaxLength(500);
            entity.Property(e => e.MensajeError).HasMaxLength(1000);
            entity.HasIndex(e => e.OrdenVentaId).IsUnique();
            entity.HasIndex(e => new { e.Serie, e.Numero }).IsUnique();
        });

        modelBuilder.Entity<FacturaDetalle>(entity =>
        {
            entity.ToTable("FacturaDetalles");
            entity.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UnidadMedida).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Cantidad).HasPrecision(18, 4);
            entity.Property(e => e.ValorUnitario).HasPrecision(18, 2);
            entity.Property(e => e.PrecioUnitario).HasPrecision(18, 2);
            entity.Property(e => e.Descuento).HasPrecision(18, 2);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.Igv).HasPrecision(18, 2);
            entity.Property(e => e.Total).HasPrecision(18, 2);
            entity.HasOne(e => e.Factura)
                .WithMany(e => e.Detalles)
                .HasForeignKey(e => e.FacturaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuiaRemision>(entity =>
        {
            entity.ToTable("GuiasRemision");
            entity.Property(e => e.NumeroGuia).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Serie).HasMaxLength(10).IsRequired();
            entity.Property(e => e.EstadoEmision).HasMaxLength(30).IsRequired();
            entity.Property(e => e.PesoBrutoTotal).HasPrecision(18, 3);
            entity.Property(e => e.PuntoPartidaDireccion).HasMaxLength(250).IsRequired();
            entity.Property(e => e.PuntoLlegadaDireccion).HasMaxLength(250).IsRequired();
            entity.Property(e => e.ConductorNombre).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PlacaVehiculo).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.OrdenVentaId).IsUnique();
            entity.HasIndex(e => new { e.Serie, e.Numero }).IsUnique();
            entity.HasOne(e => e.OrdenVenta).WithMany(o => o.GuiasRemision).HasForeignKey(e => e.OrdenVentaId);
            entity.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.ClienteId);
        });

        modelBuilder.Entity<GuiaRemisionDetalle>(entity =>
        {
            entity.ToTable("GuiaRemisionDetalles");
            entity.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UnidadMedida).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Cantidad).HasPrecision(18, 4);
            entity.Property(e => e.PesoUnitarioKg).HasPrecision(18, 3);
            entity.Property(e => e.PesoTotalKg).HasPrecision(18, 3);
            entity.HasOne(e => e.GuiaRemision).WithMany(g => g.Detalles).HasForeignKey(e => e.GuiaRemisionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Producto).WithMany().HasForeignKey(e => e.ProductoId);
        });
        
        // Relación 1 a 1 de Cotizacion y OrdenVenta
        modelBuilder.Entity<Cotizacion>()
            .HasOne(c => c.OrdenVenta)
            .WithOne(o => o.Cotizacion)
            .HasForeignKey<OrdenVenta>(o => o.CotizacionId);

        modelBuilder.Entity<OrdenVenta>()
            .Property(e => e.Revision)
            .IsConcurrencyToken();
    }
}
